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
3. 比較対象に欠損がある場合は `Mismatched`
4. 比較対象が全て存在し、値が全て等しいなら `Matched`
5. それ以外は `Mismatched`

### 3.2 Value Path Level (`generated` / `dynamic`)

`Property` の最終値を比較対象として同じルールを適用する。

- `null` 同士は一致
- `null` と非 `null` は不一致

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

