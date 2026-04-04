# 2026-04-04 Typed Projection Follow-up Tracking

## Summary

- `AsDynamic()` は機能として成立したが、`dynamic` 連鎖により IDE 補完と型安全が弱い点を次回課題として登録。
- 今回は動作優先で現機能を採用し、フォローアップを明示的に追跡する。

## Tracking Actions

- GitHub:
  - PR #9 にフォローアップコメントを追加
  - Comment ID: `4186407883`
- Project task tracking:
  - `tasks/tasks-status.md` Backlog に `T-040` を追加
  - 内容: 型付き投影 API の設計・実装（補完性改善）

## Next Scope (T-040)

- 目的: `root.Groups[0].Items[0].MetricA[0]` 相当の記述で補完を効かせる
- 条件:
  - `list index -> model index` の意味維持
  - `GetState(modelIndex)` による Missing/Null 判定維持
  - 既存 `AsDynamic()` との互換を壊さない
