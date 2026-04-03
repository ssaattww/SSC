# Phases Status

- Updated: 2026-04-03

## Phase 1: 要件・外部仕様確定

- Status: Done
- Notes:
  - 詳細設計粒度（L3）を決定済み
  - 外部仕様の主要項目を確定済み（CompareKey 無し List、重複キー、アクセス順）
  - `null`/欠損は `GetState` 追加で区別する仕様で確定

## Phase 2: 詳細設計確定

- Status: In Progress
- Notes:
  - `doc/DetailDesignDraft.md` に粒度基準・章別必須項目・仕様確定結果を追記
  - コンテナ対応（Dictionary / IEnumerable）と SelectMany 仕様を確定
  - 詳細 API 定義（`Result`/`CompareConfiguration`/エラー型）を次ステップで確定予定

## Phase 3: 実装

- Status: Not Started

## Phase 4: 検証・受け入れ

- Status: Not Started
