# SelectMany 例の構成明確化レポート

- Date: 2026-04-03
- Target: `doc/draft/DetailDesignDraft.md`

## 対応内容

`18.6 SelectMany の例` を以下の3ブロックに分割した。

1. `18.6.1 ユーザー側モデル`
- `Dataset`, `Group`, `Item` をユーザー定義型として分離

2. `18.6.2 ライブラリ公開型`
- `Parallel<T>`, `ParallelDataset`, `ParallelGroup`, `ParallelItem`, `Api.Compare(...)` をライブラリ提供型として分離

3. `18.6.3 利用コード`
- `Compare` 呼び出し後にライブラリ型へ入る境界をコメントで明示
- `SelectMany` の各段での型変換を明示

## 目的

- 「どこまでがユーザーのデータ型か」を即座に判別できるようにする
- 実装と利用時の責務分離を設計書上で固定する

