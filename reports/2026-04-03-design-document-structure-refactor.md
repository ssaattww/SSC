# 設計書構成再編レポート

- Date: 2026-04-03
- Scope: 設計書の Draft/非Draft 分離と詳細設計の分割

## 実施内容

1. 文書系統の分離
- `doc/draft/DetailDesignDraft.md` にドラフトを集約
- `doc/design/` を非Draft（確定設計）として新設

2. 基本設計の移設
- `doc/design/basic/BasicDesign.md` へ移動

3. 詳細設計の分割
- `doc/design/detail/` に機能粒度で分割
  - `00-Overview.md`
  - `01-DomainModel.md`
  - `02-PublicApi.md`
  - `03-ContainerRules.md`
  - `04-SelectManySemantics.md`
  - `05-ResultAndErrors.md`
  - `06-ExecutionPipeline.md`
  - `07-NonFunctional.md`
  - `08-ImplementationChecklist.md`

4. インデックス整備
- `doc/README.md`
- `doc/design/README.md`

## 補足

- 既存 `tasks/reports` 内のパス参照は `doc/draft/DetailDesignDraft.md` へ更新済み
