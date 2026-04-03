# Phases Status

- Updated: 2026-04-03

## Phase 1: 要件・外部仕様確定

- Status: Done
- Notes:
  - 詳細設計粒度（L3）を決定済み
  - 外部仕様の主要項目を確定済み（CompareKey 無し List、重複キー、アクセス順）
  - `null`/欠損は `GetState` 追加で区別する仕様で確定

## Phase 2: 詳細設計確定

- Status: Done
- Notes:
  - `doc/draft/DetailDesignDraft.md` に粒度基準・章別必須項目・仕様確定結果を追記
  - Draft/非Draft を `doc/draft` と `doc/design` に分離
  - 非Draft の詳細設計を `doc/design/detail` に機能粒度で分割
  - コンテナ対応（Dictionary / IEnumerable）と SelectMany 仕様を確定
  - コンテナ対応・SelectMany の具体例を設計書に追記
  - SelectMany の型付き例を追記
  - SelectMany 例をユーザー型/ライブラリ型/利用コードに分離
  - SelectMany 展開結果の具体データをコードコメントで明示
  - SelectMany の各要素が model スロットを持つ点をコメントで明示
  - `Result`/`CompareConfiguration`/IssueCode を詳細設計へ反映
  - 非Draft詳細設計を章ごとに内容拡充（契約・例・実行手順を明文化）

## Phase 3: 実装

- Status: Not Started

## Phase 4: 検証・受け入れ

- Status: Not Started

