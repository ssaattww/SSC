# Container Mapping Rules

## Supported Containers

1. `IDictionary<TKey,TValue>` / `IReadOnlyDictionary<TKey,TValue>`
2. `IList<T>` / `IReadOnlyList<T>` / `T[]`
3. `IEnumerable<T>`（実行時に上記へ解決可能な場合）

## Dictionary Rules

- `TKey` を比較キーとして使用（`CompareKey` 不要）
- keyUnion を作り `Parallel<TValue>` へ正規化

## Sequence Rules

- 要素型に `CompareKey` が必須
- CompareKey 無しは `Skip + Error`（strict で例外）
- 重複キーは `Error`（strict で例外）

## IEnumerable Rules

- Compare 開始時に 1 回だけマテリアライズ
- 対応不能な one-shot 列挙は `UnsupportedContainerType`

## Unsupported

- `IAsyncEnumerable<T>`
