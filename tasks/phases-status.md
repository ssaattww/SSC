# Phases Status

- Updated: 2026-04-04

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
  - 実装前確認事項 6 点（例外分離、Path/Key例示、CompareIgnore、性能受け入れ、TDD/E2E）を確定
  - Composite Key の説明を具体例で明確化

## Phase 3: 実装

- Status: In Progress
- Notes:
  - 実装着手前に CI/CD 基盤（.github workflows）をリポジトリ構成へ適合化
  - 初版まではテスト workflow のみ有効、release publish は無効化
  - 最小実装スケルトンを基盤に E2E テストを拡張（StringComparison、3 model slot、Issue診断情報、IEnumerable 1回列挙）
  - 既存テスト（E2E/Unit）へ意図コメントを追記し、仕様意図を明示
  - `CompareKeyValueIsNull` の Issue `KeyText` を `<null>` で可視化
  - OrdinalIgnoreCase 同値の同一モデル内辞書キー衝突を `DuplicateCompareKeyDetected` として検知
  - sequence element が null の場合も `CompareKeyValueIsNull.KeyText` を `<null>` で統一
  - サブエージェントレビューを実施し、重大度 Critical/High/Medium の指摘なしを確認
  - レビュー指摘の考慮漏れ論点（文字列キー比較/KeyText 表示）を `doc/design/detail` に明文化
  - `dotnet test SSC.sln --configuration Release` は成功（E2E 17件 / Unit 2件）

## Phase 4: 検証・受け入れ

- Status: Not Started
