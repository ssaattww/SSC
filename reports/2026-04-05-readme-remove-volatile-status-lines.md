# README Volatile Status Line Removal

- Date: 2026-04-05
- Scope: `README.md`

## Background

README の `Status` 節に以下の可変情報が含まれていた。

- current phase
- latest test pass counts

これらは更新頻度が高く、README の保守コストと記載劣化リスクが高い。

## Change

`README.md` から次の行を削除。

- `Current phase: ...`
- `Test status (latest): ...`
- `E2E: ...`
- `Unit: ...`

`Target framework` は固定情報として維持。

## Result

README は長期的に変化しにくい情報に絞られ、メンテナンス負荷を低減。

