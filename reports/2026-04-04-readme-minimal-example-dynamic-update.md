# README Minimal Example Dynamic Update

- Date: 2026-04-04
- Goal: `README.md` の Minimal Example を現行 API 導線に合わせる

## 変更内容

- Minimal Example を `ParallelNode` 直接アクセスから `CompareResult` 入口の dynamic 導線へ更新
  - 変更前: `result.Root` を `ParallelNode<T>` へキャストして `GetChildren(...)`
  - 変更後: `dynamic root = result.AsDynamic()!` から `root.Items[index].Price[modelIndex]`
- `GetState(modelIndex)` の参照例を追加し、Missing の扱いを明示
