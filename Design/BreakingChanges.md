# Breaking Changes

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
