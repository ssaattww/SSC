# ValueState Behavior

## 1. Purpose

`GetState(modelIndex)` の返却値を比較用途に特化して単純化する。

## 2. Public State

```csharp
public enum ValueState
{
    Missing,
    Matched,
    Mismatched,
}
```

- `Missing`
  - 当該 model slot が欠損
  - または比較対象が存在しない
- `Matched`
  - 当該 slot が存在し、比較対象と一致
- `Mismatched`
  - 当該 slot が存在し、比較対象と不一致（比較先欠損を含む）

## 3. Evaluation Rule

### 3.1 Node Level (`ParallelNode<T>.GetState`)

1. 当該 slot が欠損なら `Missing`
2. 比較対象モデルが存在しないなら `Missing`
3. scalar node では、比較対象に欠損がある場合は `Mismatched`
4. scalar node では、比較対象が全て存在し、値が全て等しいなら `Matched`
5. scalar node では、それ以外は `Mismatched`
6. object/container node では、self presence mismatch または subtree 差分があれば `Mismatched`
7. object/container node では、self presence が一致し、配下 child/member に差分が無ければ `Matched`

node level の object/container 判定では、model object 自体の参照同値は使わない。

- `new Detail { Value = 1 }` と別インスタンスの `new Detail { Value = 1 }` は、配下 member が一致していれば `Matched`
- object/container node の不一致判定は、node 自身の `Missing/PresentNull/PresentValue` と配下 subtree の差分有無から導く

### 3.2 Value Path Level

#### 3.2.1 Dynamic

`AsDynamic()` から辿る `Property` の最終値を比較対象として、node level と同じルールを適用する。

- `null` 同士は一致
- `null` と非 `null` は不一致
- T-042 では dynamic value-path `GetState(modelIndex)` の state lookup を compare 時に materialize した member state に切り替える
- そのため `GetState` 呼び出し中に member getter を再実行してはならない
- 観測可能な timing change は比較実行時に限定し、getter の副作用や例外は compare / node construction 側へ前倒しされ得る
- この non-invasive 保証は materialize 済み member に限定する
- declared type に存在しない runtime-derived 追加メンバーは dynamic access 自体は継続利用できるが、`GetState` は legacy runtime reflection fallback を使うため T-042 の保証対象外とする
- runtime-derived 追加メンバーが container の場合も、member access 自体は list view として継続利用できる
- runtime-derived container の正規化前提を満たさない場合、container access は `CompareExecutionException` で失敗する

#### 3.2.2 Generated

generated projection でも公開 `ValueState` の意味は同じである。

- `null` 同士は一致
- `null` と非 `null` は不一致
- ただし T-042 の設計変更範囲は dynamic value path に限定し、generated nested value path の non-invasive `GetState` 化は対象外とする

## 4. Examples

2モデル比較で `left/right` の状態を示す。

| left | right | left.GetState(0) | right.GetState(1) |
| --- | --- | --- | --- |
| missing | missing | Missing | Missing |
| missing | 10 | Missing | Mismatched |
| 10 | missing | Mismatched | Missing |
| null | null | Matched | Matched |
| 10 | 10 | Matched | Matched |
| 10 | 20 | Mismatched | Mismatched |

## 5. Internal Representation

公開 `ValueState` とは別に、内部評価では `NodePresenceState` を使用する。

- `Missing`
- `PresentNull`
- `PresentValue`

これにより以下を両立する。

- 公開 API の単純な3状態
- 比較時の `missing/null/value` 判定精度
