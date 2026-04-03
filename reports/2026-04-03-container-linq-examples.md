# コンテナ対応・SelectMany 例示追記レポート

- Date: 2026-04-03
- Target: `doc/DetailDesignDraft.md`

## 追記した例示

1. コンテナ対応（章 17.6）
- Dictionary の keyUnion と欠損並列化例
- List/配列の `CompareKey` 正規化例
- `IEnumerable<T>` 宣言時の実行時型分岐例

2. `SelectMany` 仕様（章 18.5）
- `Groups -> Items` の 2 段階展開例
- 欠損を含む場合の展開ルール例
- モデル軸を展開しないことの確認例

## 目的

- 実装担当が順序・欠損・展開単位の誤解なく実装できる状態にする
