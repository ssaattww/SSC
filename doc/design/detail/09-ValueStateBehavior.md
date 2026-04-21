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
- 比較時に事前構築済みのメンバーでは、`GetState(modelIndex)` は compare 時に保存した member state を読む
- そのため、その経路では `GetState` 呼び出し中に member getter を再実行しない
- この保証により、getter の副作用や例外は compare / node construction 側へ前倒しされ得る
- この保証は、プロパティ宣言型から辿れて compare 時に事前構築済みになっているメンバーに限定する
- プロパティ宣言型に無い実行時専用メンバーは dynamic access 自体は継続利用できるが、`GetState` は呼び出し時の反射による代替解決で判定する
- そのため、実行時専用メンバーの `GetState` は「呼べることがある」が、「getter を再実行しない」保証は持たない
- 比較相手 model が 1 つも無い場合、実行時専用メンバーの `GetState` は `Missing` を返す
- 対象 model でも他 model でも、member path の途中に対象メンバーが存在しない場合、`GetState` は `MissingMemberException` で失敗し得る
- getter 自体が例外を投げる場合、その例外は `GetState` 呼び出し側へ伝播する
- 実行時専用メンバーが container の場合は member access 自体を list view として継続利用できる
- ただし実行時専用 container の正規化前提を満たさない場合、container access は `CompareExecutionException` で失敗する
- 通常経路と代替経路の段階的な分岐は `02-PublicApi` と `06-ExecutionPipeline` に記載する

#### 3.2.2 Generated

generated projection でも公開 `ValueState` の意味は同じである。

- `null` 同士は一致
- `null` と非 `null` は不一致
- ただし getter を再実行しない `GetState` 保証は dynamic value path に限定し、generated nested value path の同等保証は対象外とする

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
