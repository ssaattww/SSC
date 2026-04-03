# Tasks Status

- Updated: 2026-04-03

## In Progress

- なし

## Backlog

- なし

## Done

- T-001: 詳細設計の粒度決定
  - Status: 完了（L3 粒度と外部仕様 1-5 を確定）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 10-16）
    - `reports/2026-04-03-detail-design-granularity.md`
    - `reports/2026-04-03-external-spec-resolution.md`
- T-002: コンテナ対応と SelectMany 仕様の確定
  - Status: 完了（Dictionary / IEnumerable 方針と SelectMany の順序・欠損挙動を確定）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 17-18）
    - `reports/2026-04-03-container-and-linq-spec.md`
- T-003: コンテナ対応と SelectMany の具体例追記
  - Status: 完了（ドラフト同等粒度の例示を設計書へ反映）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 17.6, 18.5）
    - `reports/2026-04-03-container-linq-examples.md`
- T-004: SelectMany の型付き例追記
  - Status: 完了（型定義・入力データ・クエリ結果型を全明示）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 18.6）
    - `reports/2026-04-03-selectmany-typed-example.md`
- T-005: SelectMany の型境界を明確化
  - Status: 完了（ユーザー型とライブラリ型を分離して記述）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 18.6.1-18.6.3）
    - `reports/2026-04-03-selectmany-boundary-clarification.md`
- T-006: SelectMany 結果データのコメント追記
  - Status: 完了（展開後の `groups` / `items` の具体内容をコードコメントで明示）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 18.6.3）
    - `reports/2026-04-03-selectmany-result-comment.md`
- T-007: SelectMany 要素の model 単位性を明示
  - Status: 完了（各要素が `ParallelXxx` として model スロットを持つことをコメントで明示）
  - Output:
    - `doc/draft/DetailDesignDraft.md`（章 18.6.3）
    - `reports/2026-04-03-selectmany-model-slot-clarification.md`
- T-008: Draft/非Draft 設計書の分離
  - Status: 完了（`doc/draft` と `doc/design` に再編）
  - Output:
    - `doc/README.md`
    - `doc/draft/DetailDesignDraft.md`
    - `doc/design/basic/BasicDesign.md`
    - `doc/design/README.md`
- T-009: 詳細設計の粒度分割
  - Status: 完了（詳細設計を機能粒度でファイル分割）
  - Output:
    - `doc/design/detail/00-Overview.md`
    - `doc/design/detail/01-DomainModel.md`
    - `doc/design/detail/02-PublicApi.md`
    - `doc/design/detail/03-ContainerRules.md`
    - `doc/design/detail/04-SelectManySemantics.md`
    - `doc/design/detail/05-ResultAndErrors.md`
    - `doc/design/detail/06-ExecutionPipeline.md`
    - `doc/design/detail/07-NonFunctional.md`
    - `doc/design/detail/08-ImplementationChecklist.md`
    - `reports/2026-04-03-design-document-structure-refactor.md`
- T-010: 非Draft詳細設計の内容拡充
  - Status: 完了（骨子レベルから実装準備レベルへ詳細化）
  - Output:
    - `doc/design/detail/00-Overview.md`
    - `doc/design/detail/01-DomainModel.md`
    - `doc/design/detail/02-PublicApi.md`
    - `doc/design/detail/03-ContainerRules.md`
    - `doc/design/detail/04-SelectManySemantics.md`
    - `doc/design/detail/05-ResultAndErrors.md`
    - `doc/design/detail/06-ExecutionPipeline.md`
    - `doc/design/detail/07-NonFunctional.md`
    - `doc/design/detail/08-ImplementationChecklist.md`
    - `reports/2026-04-03-final-design-content-enrichment.md`

