# SelectMany 型付き例 追記レポート

- Date: 2026-04-03
- Target: `doc/draft/DetailDesignDraft.md`

## 追記内容

1. `Parallel<T>` の基本インタフェース
2. `ParallelDataset` / `ParallelGroup` / `ParallelItem` の型定義
3. `Dataset` / `Group` / `Item` のデータモデル定義
4. `data0` / `data1` の具体データ初期化
5. `Compare` 呼び出しから `SelectMany` 2段展開までの変数型明示

## 目的

- `SelectMany` 実行時に何が `IEnumerable<ParallelXxx>` になるかを曖昧なく示す
- `[modelIndex]` 参照が展開後も維持されることを型レベルで明示する

