# Non-Functional Design

## Determinism

- 同一入力は同一順序で列挙
- 文字列キー比較は `StringComparison.Ordinal`

## Reflection Cache

- `ConcurrentDictionary<Type, TypeMetadata>`
- メタデータ生成後は不変

## Enumerable Safety

- `IEnumerable<T>` は 1 回だけ `List<T>` に変換
- 比較中の再列挙禁止

## Memory Policy

- 比較中に必要な中間マップのみ保持
- keyUnion 確定後は不要中間データを破棄
