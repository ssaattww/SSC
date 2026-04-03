# コンテナ対応と LINQ 仕様確定レポート

- Date: 2026-04-03
- Target: `doc/draft/DetailDesignDraft.md`

## 決定事項

1. Dictionary 系コンテナ
- `IDictionary<TKey,TValue>` / `IReadOnlyDictionary<TKey,TValue>` を正式対応
- `TKey` を比較キーとして使う（`CompareKey` 不要）

2. List/配列系コンテナ
- 要素型に `CompareKey` がある場合のみ正規化
- 無い場合は `Skip + Result エラー`（strict で例外可）

3. `IEnumerable<T>` 宣言プロパティ
- 受け付ける
- Compare フェーズで一度だけマテリアライズして処理
- 実行時型が非対応コンテナなら `Skip + Result エラー`

4. `SelectMany` 仕様
- モデル軸ではなく、構造の 1 階層を展開
- 順序は「外側要素順 -> 内側 keyUnion 順」
- 親が全欠損なら子は空列

## 補足

- `IAsyncEnumerable<T>` と one-shot 列挙体は非対応として明示エラー化

