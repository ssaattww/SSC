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
  - `OrdinalIgnoreCase` 時の `KeyText` を同値候補の Ordinal 最小表記へ正規化し、診断表示の決定性を強化
  - 厳密化差分に対してサブエージェント再レビューを実施し、Low リスクのみ記録
  - モデル直下 List メンバーについて、両 model 複数要素ケースの E2E を追加し、union/slot を明示検証
  - トップレベル README を整備し、実装状況・利用導線・基本例を明文化
  - NuGet publish workflow を再有効化し、`release` 公開時の自動配布を復帰
  - 再有効化差分をサブエージェントレビューし、release タグ版数と NuGet 版数の整合ロジックを追加
  - `publish-nuget.yml` を流用し、`main` への push から pre-release 作成 -> publish 連携を自動化
  - レビュー指摘に基づき、`main` push ラン内で pre-release publish を完結する構成へ修正
  - stable tag 解決で `v` プレフィックス付きタグ互換を追加
  - NuGet パッケージ識別子を `ssaattww.SSC` へ明示変更し、公開時の名前衝突を回避
  - PackageId 変更差分をレビューし、NuGet readme メタデータ不足を残留課題として記録
  - ルートREADMEへ配布導線バッジを追加し、NuGet package readme として同梱設定を反映
  - READMEレビュー指摘を反映し、ライセンスバッジリンクを絶対URLへ補正
  - `dotnet test SSC.sln --configuration Release` は成功（E2E 18件 / Unit 2件）
  - 次回作業再開を支援するリポジトリ内スキル `hikitsugi` の基盤を追加
  - 新規チャット再開用の詳細ハンドオーバーレポートを作成
  - `hikitsugi` の `SKILL.md` をベストプラクティス準拠の手順型に再編
  - List メンバーの参照方法を `Items[index] x modelIndex` としてテスト・設計書へ明示
  - 階層アクセスを簡潔化する `Children(model => model.Member)` API を追加し、深い階層の可読性を改善
  - `02-PublicApi` に比較前の元データセット例を追加し、アクセス例の前提と型定義を明示
  - `02-PublicApi` のアクセス例を元データセットと同じ型・値軸へ修正し、例示の整合性を担保
  - `02-PublicApi` に概念表現（`root.Groups[0].Items[0].MetricA[0]`）と null 判定指針を補足
  - `AsDynamic()` を追加し、`root.Groups[0].Items[0].MetricA[0]` と `root.Groups[0].Items[0][0]` 形式の実アクセスを提供
  - 補完性向上のため、次回課題として型付き投影 API（T-040）を Backlog 登録
  - dynamic projection 差分をサブエージェント再レビューし、追跡タスク（T-041）で公開契約整合（`AsDynamic` 入口）/ `GetState` 意味整合 / 予約名衝突 / 境界例外 / `CreateLeaf` 引数検証を実装完了
  - T-041 完了後の再レビューで High/Medium の新規指摘なしを確認し、`value path GetState` 非侵襲化は残留課題（T-042）として Backlog 化
  - 新規チャット再開向けに `reports/chat-handover-for-new-thread-20260404_144004.md` を作成し、最新状態を8章構成で整理
  - dynamic 依存低減について補完性/性能観点の方式評価を実施し、段階移行案（C -> A -> B）を `reports/2026-04-04-typed-access-approach-evaluation.md` に整理
  - 方針を更新し、最終到達点を Source Generator 案（案3）へ設定したロードマップを `reports/2026-04-04-source-generator-roadmap.md` に記録
  - subagent 設計レビュー結果を反映し、配布戦略（runtime/generator 分離）と generated API 契約（scope/命名/失敗契約/Dictionary表現）を設計へ明記
  - `src/SSC.Generators` を追加し、`[GenerateParallelView]` 付き型に対する `AsGeneratedView()` と型付き view 生成を実装
  - generated projection の E2E を追加し、`dotnet test SSC.sln --configuration Release` 成功を確認（E2E 25件 / Unit 4件）
  - runtime コード生成の選択肢（Reflection.Emit / Roslyn Compilation / 事前生成切替）と Roslyn Scripting との差分をレポート化
  - PR テスト workflow を拡張し、xUnit 実行前に source generator プロジェクトも build 検証するよう更新
  - `CompareResult` 拡張を追加し、`ParallelCompareApi.Compare(models).AsDynamic()` / `AsGeneratedView()` で投影方式を早い段階で切替可能に更新（`result.Root!...` 導線は廃止）
  - 上記差分を subagent でレビューし、doc の可視性表記（public/internal）不一致と旧 dynamic サンプル導線を修正
  - NuGet publish workflow を2パッケージ同時配布へ更新し、`ssaattww.SSC` と `ssaattww.SSC.Generators` に同一バージョンを適用
  - ルート README に Source Generator 対応と2パッケージ利用導線を追記し、`SSC.Generators` パッケージにも README を同梱
  - NuGet metadata（repository/license）を両パッケージへ追加し、README に generator downloads バッジを追加
  - README の Minimal Example を `result.AsDynamic()` 入口の最新 dynamic 導線へ更新

## Phase 4: 検証・受け入れ

- Status: Not Started
