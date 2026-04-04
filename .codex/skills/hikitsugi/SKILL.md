---
name: hikitsugi
description: 現在スレッドの作業内容を次回再開しやすい形に整理し、進捗・未解決事項・次アクションを引き継ぎ文書へまとめるときに使う。
---

# Hikitsugi

## Overview

このスキルは、作業終了時やスレッド移行時に、次回の担当者がすぐ再開できる引き継ぎ情報を作成するための手順を提供する。

## When To Use

- スレッドを切り替える前に、進捗と未解決事項を整理したいとき
- 実装・レビュー・運用変更の履歴を、次回作業向けに要約したいとき
- 次回開始時の確認ファイル（`reports/`, `tasks/`）を明示したいとき

## Workflow

1. 現在の差分と直近コミットを確認して、実施済み作業を確定する。
2. `reports/` に日付付きの引き継ぎレポートを作成し、以下を記録する。
   - 完了事項
   - 未完了事項
   - 注意点（仕様・運用・CI/CD・公開手順など）
3. `tasks/tasks-status.md` と `tasks/phases-status.md` を最新化する。
4. ユーザーからの運用上の指摘があれば `tasks/feedback-points.md` に追記する。
5. 次回の開始手順を、対象ファイルパス付きで明示する。

## Output Checklist

- `reports/YYYY-MM-DD-*.md` に引き継ぎ内容を保存した
- `tasks/tasks-status.md` のタスク状態を更新した
- `tasks/phases-status.md` のフェーズ状態を更新した
- `tasks/feedback-points.md` に新規指摘を反映した（該当時のみ）
- 次回開始時に最初に読むファイルを明記した

## Prompt Placeholder

詳細プロンプトは後で追加する前提とし、当面は `agents/openai.yaml` の `default_prompt` を起点に利用する。
