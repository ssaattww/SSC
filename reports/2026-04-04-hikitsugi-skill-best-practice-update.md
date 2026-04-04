# 2026-04-04 Hikitsugi Skill Best-Practice Update

## Summary

- `.codex/skills/hikitsugi/SKILL.md` を、単一の依頼文テンプレート配置から、再利用可能なスキル手順型へ再構成。
- ユーザーが追加した要件（8章構成・要約禁止・情報欠落防止）を維持したまま、trigger / output contract / workflow / quality gate を明確化。

## Why Updated

- 旧版は実行プロンプト本文が中心で、スキルとしての発火条件と品質基準が弱く、再利用時の一貫性が低かった。
- skill-creator のベストプラクティス（簡潔、手順化、品質ゲート、曖昧性排除）に合わせるため。

## Changes

- Frontmatter description を trigger しやすい文に更新。
- 本文を以下の構成へ再編。
  - `Purpose`
  - `Trigger`
  - `Output Contract`（8章固定、ファイル命名優先順）
  - `Workflow`
  - `Quality Gate`
  - `Do Not`
- ファイル名規約を「ユーザー指定優先、未指定時は既定名」に統一。

## Validation

- Command:
  - `python3 /home/ibis/.codex/skills/.system/skill-creator/scripts/quick_validate.py /home/ibis/dotnet_ws/SSC/.codex/skills/hikitsugi`
- Result:
  - `Skill is valid!`

## Affected Files

- `.codex/skills/hikitsugi/SKILL.md`
- `reports/2026-04-04-hikitsugi-skill-best-practice-update.md`
