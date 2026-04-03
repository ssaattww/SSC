# SelectMany model スロット明確化レポート

- Date: 2026-04-03
- Target: `doc/draft/DetailDesignDraft.md`

## 追記内容

- 章 18.6.3 の `groups` と `items` コメントに以下を明示
  - 各要素は `ParallelGroup` / `ParallelItem`（1要素=複数modelの束）
  - `element[i][0]` が model0、`element[i][1]` が model1 の値
  - `SelectMany` 後も model スロット構造は維持される

## 目的

- `SelectMany` を実行しても `IEnumerable<Item>` にはならず、
  `IEnumerable<ParallelItem>` のままであることを誤解なく伝える

