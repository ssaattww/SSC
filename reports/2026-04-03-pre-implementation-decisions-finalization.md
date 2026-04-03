# 実装前確認事項 確定レポート

- Date: 2026-04-03
- Scope: `doc/design/detail/*.md`

## 確定内容

1. 例外型は分離する
- 入力系: `CompareInputException`
- 実行系: `CompareExecutionException`

2. `Path` 仕様
- `.` 区切りのプロパティチェーン
- 例: `Dataset.Groups.Items.MetricA`

3. キー比較仕様（例示付き）
- string: `Ordinal`
- DateTime: UTC 正規化の上で比較
- 複合キー: 構成順序を固定

4. 反射対象の制御
- 制限は最小化
- 除外は `CompareIgnore` 属性で明示

5. 性能受け入れ基準
- 本フェーズは「正しく動作すること」を優先
- 固定閾値は設けない

6. テスト方針
- TDD を基本
- E2E 重視で仕様連結を検証

## 反映先

- `00-Overview.md`
- `01-DomainModel.md`
- `02-PublicApi.md`
- `03-ContainerRules.md`
- `05-ResultAndErrors.md`
- `06-ExecutionPipeline.md`
- `07-NonFunctional.md`
- `08-ImplementationChecklist.md`
