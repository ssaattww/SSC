【1. このチャットの目的】
- `doc` 配下に置かれた基本設計/詳細設計ドラフトを基に、詳細設計の粒度を決め、外部仕様を確定し、実装に入れる状態まで設計を詰めること。
- 設計ドキュメント運用を整備すること。
  例: `draft` と確定設計の分離、ファイル分割、タスク/フェーズ/フィードバック管理、レポート化、ブランチ運用、こまめなコミット。
- 最終ゴールは「このリポジトリで、実装フェーズに迷わず移れる設計セットと運用ルールを揃えること」。

【2. 新しいチャットに切り替える理由】
- 会話が長くなり、決定事項・運用ルール・コミット/PR履歴が多層化したため。
- 途中で論点が設計内容、文書構成、命名ルール、CI/release、PR運用まで拡大しており、次の作業で参照しづらくなっているため。
- 次チャットでは「未マージPR対応」または「Phase 3実装着手」に即入れるよう、前提を一本化したい。

【3. 背景・前提条件】
- リポジトリ: `C:\Users\taiga\source\repos\ssaattww\SSC`
- 現在の主要ブランチ:
  - 作業ブランチ: `codex/detail-design-granularity`
  - ベース: `main`
- 重要運用ルール（会話中に確定/追記済み）:
  - ブランチ分けて作業する。
  - こまめにコミットする。
  - 開発方法の指摘は `tasks/feedback-points.md` に必ず記録。
  - 成果物名/見出しに「指摘時の言い回し」をそのまま使わない（中立名にする）。
  - PRメッセージは文字化け回避のためテキストファイルから入力する。
- AGENTS.md 由来の管理ルール:
  - `tasks/tasks-status.md`, `tasks/phases-status.md`, `tasks/feedback-points.md` を更新し続ける。
- 設計運用ルール:
  - `doc/draft` は議論/ドラフト。
  - `doc/design` は確定設計（実装参照の正本）。
- 環境上の実務的注意:
  - Git書き込み系コマンドは権限確認が発生しやすい。
  - コマンド形式を固定して確認回数を最小化する方針で運用済み。
- ユーザー指示で確定した方針:
  - 「困らない内容は止まらず進めてよい」。
  - 初版リリース前は「テストのみ有効、releaseはしない」。

【4. ここまでの経緯】
- 1) `doc/BasicDesign.md`, `doc/DetailDesignDraft.md` を確認し、詳細設計粒度を L3（実装直結）に決定。
- 2) `tasks` と `reports` が無かったため新規作成し、管理ファイルを開始。
- 3) 外部仕様を対話で確定:
  - CompareKeyなしListは `Skip + Resultエラー`（strictで例外可）
  - 重複CompareKeyはエラー（strictで例外可）
  - アクセスは `Group[index][model]` 志向
  - 欠損/nullは区別（`GetState` 追加）
  - 性能上限は固定しない
- 4) コンテナ対応と SelectMany 仕様を拡張:
  - Dictionary対応
  - IEnumerable対応（1回マテリアライズ）
  - SelectManyの順序・欠損・model slot保持を明文化
- 5) SelectManyの説明改善を複数回実施:
  - 型省略なし例
  - ユーザー型/ライブラリ型/利用コードの責務分離
  - 展開結果コメント追記
  - 各要素が modelごとの束であることを明示
- 6) 命名品質改善:
  - 「省略なし」等、指摘文言が成果物名に残る問題を修正
  - AGENTS.md に命名ルール追記
- 7) 設計文書再編:
  - `doc/draft` と `doc/design` に分離
  - `doc/design/detail` を9ファイルに分割
- 8) 非Draft設計が薄い指摘を受け、内容を大幅拡充（合計行数を 268→545 へ）。
- 9) 実装前確認6点を反映:
  - 例外分離、Path例示、キー比較例示、CompareIgnore属性、性能受け入れ、TDD/E2E
- 10) Composite Key説明の分かりにくさを修正:
  - 「複合キーは全キー一致が必要」を明記
- 11) PR運用:
  - PR #1 作成・本文をファイル入力で更新。
  - ユーザーが先に #1 をマージ。
  - 未反映1コミット（workflow修正）を追いPR #2 として作成。
- 12) `.github` 適合化:
  - PRテスト workflow: `main` 対応 + test csproj自動検出
  - publish workflow: 初版まで無効化（手動 + `enable_release=true` 時のみ実行）

【5. 決定事項】
- 設計粒度:
  - 詳細設計は L3（実装直結）。
- 外部仕様:
  - CompareKeyなしList: `Skip + Error`（strictなら例外）
  - 重複CompareKey: Error（strictなら例外）
  - 参照モデル: `index -> model` (`Group[index][model]`)
  - 欠損/nullは区別し、`GetState(modelIndex)` で3値判定
- コンテナ:
  - Dictionary正式対応（`TKey`利用）
  - List/配列は CompareKey必須
  - IEnumerableは1回マテリアライズ
  - `IAsyncEnumerable` など非対応
- SelectMany:
  - 構造階層を展開、model軸は展開しない
  - 結果要素は `ParallelXxx` を維持
  - 各要素は model slot を持つ
- Composite Key:
  - 構成するキーがすべて一致しない限り一致扱いしない
- 反射対象方針:
  - 制限を増やさず、除外したいものは `CompareIgnore` 属性で除外
- 性能受け入れ:
  - 本フェーズは「動けばOK（正しさ優先）」、固定閾値なし
- テスト方針:
  - TDD + E2E重視
- ドキュメント構造:
  - 確定設計は `doc/design` を正本、`doc/draft` は判断経緯
- 運用:
  - 開発方法指摘は必ずメモ
  - PRメッセージは必ずテキストファイル入力

【6. 未解決事項・保留事項】
- 直近最大の未解決:
  - PR #2 が未マージの可能性が高い（要確認）。
  - URL: `https://github.com/ssaattww/SSC/pull/2`
- 実装フェーズ:
  - `tasks/phases-status.md` 上は Phase 3 が In Progress に入ったが、実コード実装は未着手。
- release運用:
  - 初版までは publish無効化済み。初版時に有効化手順をどう戻すかは未決定。
- ローカル状態:
  - `?? .gitignore` が未追跡として残る状態（履歴対象にするかは未判断）。
- 参考:
  - draftファイルは議論の痕跡を多く含むため、実装時は `doc/design/*` を優先する。

【7. 次のチャットで最初に依頼すべき内容】
- そのまま貼れる依頼文:
「前チャットの引き継ぎ前提で作業を再開してください。まず `origin/main` と `codex/detail-design-granularity` の差分を確認し、PR #2（https://github.com/ssaattww/SSC/pull/2）が未マージならレビュー観点を整理してマージまで進めてください。マージ済みなら Phase 3 実装に着手し、`doc/design/detail` の確定設計を正本として最小実装スケルトン（API入口、Result/Issueモデル、CompareIgnore、container正規化の枠組み、SelectManyでmodel slot維持のテスト雛形）を作成してください。運用ルールとして、ブランチ分離・こまめコミット・tasks/phases/feedback更新・PRメッセージはテキストファイル入力を守ってください。」

【8. 引き継ぎ本文】
以下を新チャット先頭に貼り付けてください。

---
このスレッドの作業を引き継いで再開してください。
リポジトリは `C:\Users\taiga\source\repos\ssaattww\SSC`、作業ブランチは `codex/detail-design-granularity` です。
前提として、設計は `doc/design` を正本、`doc/draft` を議論ログとして分離済みです。`doc/design/detail` は 9ファイルに分割し、実装準備レベルまで拡充済みです。外部仕様は確定済みで、特に以下が重要です。

- CompareKeyなしListは `Skip + Error`（strictで例外可）
- 重複CompareKeyは Error（strictで例外可）
- 参照は `Group[index][model]`
- 欠損/nullは区別し、`GetState(modelIndex)` を使う
- Dictionary対応、IEnumerableは1回マテリアライズ
- SelectManyは構造展開のみで model slot を維持
- Composite Keyは「構成キーがすべて一致したときのみ一致」
- 反射対象は広く、除外は `CompareIgnore` 属性
- 性能受け入れは当面「動けばOK」
- テスト方針は TDD + E2E重視

運用ルールも確定済みです。

- ブランチ分離で作業
- こまめにコミット
- `tasks/tasks-status.md`, `tasks/phases-status.md`, `tasks/feedback-points.md` を更新
- 開発方法に関するユーザー指摘は必ず `feedback-points` に記録
- 成果物名/見出しに指摘文言をそのまま使わない
- PRメッセージは必ずテキストファイル経由で入力

CI/releaseは `.github` をこのリポジトリ向けに調整済みです。

- `pr-xunit-tests.yml`: `main` 対応、テストcsproj自動検出
- `publish-nuget.yml`: 初版まで無効化方針（workflow_dispatch かつ `enable_release=true` のときのみ実行）

注意点として、PR #1 は既にマージ済みですが、その後の workflow 修正コミットを追いPRにしています。
PR #2: `https://github.com/ssaattww/SSC/pull/2`
まずこれが未マージかを確認してください。

次の作業優先順は以下です。

1. `origin/main` と作業ブランチ差分確認
2. PR #2 未マージならレビュー観点整理とマージ対応
3. PR #2 がマージ済みなら Phase 3 実装着手
4. 実装は `doc/design/detail` 正本に従って最小スケルトンから開始
5. 途中経過ごとに tasks/phases/feedback を更新し、こまめコミット

以上の前提で、追加確認を最小化して作業を開始してください。
---
