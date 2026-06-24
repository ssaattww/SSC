# Breaking Changes

## 2026-06-25

### T-084 Source Generator object member の生成型変更

- 対象:
  - class / struct 型の direct object member に対する generated projection
  - 例: `Document.Root`
- 変更種別:
  - generated API の source shape 変更
- 影響:
  - 従来は `Document.Root` 相当の member が `ParallelGeneratedValue<TModel, TMember>` として生成されていた
  - T-084 以降は nested generated view として生成され、`root.Root.Name` / `root.Root.Attribute` / `root.Root.Range` のように配下 member を直接辿れる
  - 旧 generated 型へ明示代入していた利用コードは source 互換でない可能性がある
- 背景:
  - gist `YXml.cs` の `Document.Root` 配下 generated member が不完全になり、Source Generator の型付き導線で class model 配下を辿れなかったため
- 備考:
  - 保守性と性能を優先し、object member は既存比較ツリーの member-node dictionary から直接 nested view 化する

## 2026-04-21

### T-070 `IParallelNode` への探索 API 追加

- 対象:
  - `IParallelNode.HasDifferences()`
  - `IParallelNode.GetDirectChildren()`
  - `ParallelChildSet`
  - `ParallelChildSet.HasDifferences`
- 変更種別:
  - public interface へのメンバー追加
- 影響:
  - `IParallelNode` を独自実装している利用者は、新メンバー実装が必要になる
  - 既存 binary / source 互換性を維持できない可能性がある
- 背景:
  - 差分のある直下子要素をライブラリ外から探索できる共通面を公開するため
- 備考:
  - T-070 は設計反映段階で breaking change として記録した
