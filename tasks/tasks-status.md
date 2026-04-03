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
    - `doc/DetailDesignDraft.md`（章 10-16）
    - `reports/2026-04-03-detail-design-granularity.md`
    - `reports/2026-04-03-external-spec-resolution.md`
- T-002: コンテナ対応と SelectMany 仕様の確定
  - Status: 完了（Dictionary / IEnumerable 方針と SelectMany の順序・欠損挙動を確定）
  - Output:
    - `doc/DetailDesignDraft.md`（章 17-18）
    - `reports/2026-04-03-container-and-linq-spec.md`
- T-003: コンテナ対応と SelectMany の具体例追記
  - Status: 完了（ドラフト同等粒度の例示を設計書へ反映）
  - Output:
    - `doc/DetailDesignDraft.md`（章 17.6, 18.5）
    - `reports/2026-04-03-container-linq-examples.md`
- T-004: SelectMany の型付き例追記
  - Status: 完了（型定義・入力データ・クエリ結果型を全明示）
  - Output:
    - `doc/DetailDesignDraft.md`（章 18.6）
    - `reports/2026-04-03-selectmany-typed-example.md`
- T-005: SelectMany の型境界を明確化
  - Status: 完了（ユーザー型とライブラリ型を分離して記述）
  - Output:
    - `doc/DetailDesignDraft.md`（章 18.6.1-18.6.3）
    - `reports/2026-04-03-selectmany-boundary-clarification.md`
- T-006: SelectMany 結果データのコメント追記
  - Status: 完了（展開後の `groups` / `items` の具体内容をコードコメントで明示）
  - Output:
    - `doc/DetailDesignDraft.md`（章 18.6.3）
    - `reports/2026-04-03-selectmany-result-comment.md`
- T-007: SelectMany 要素の model 単位性を明示
  - Status: 完了（各要素が `ParallelXxx` として model スロットを持つことをコメントで明示）
  - Output:
    - `doc/DetailDesignDraft.md`（章 18.6.3）
    - `reports/2026-04-03-selectmany-model-slot-clarification.md`
