# 外部仕様回答反映レポート

- Date: 2026-04-03
- Target: `doc/DetailDesignDraft.md`

## 回答反映

1. CompareKey 無し List
- 決定: `Skip`
- エラー: Result 返却（strict モードでは例外許可）

2. CompareKey 重複
- 決定: エラー
- エラー: Result 返却（strict モードでは例外許可）

3. アクセス順
- 決定: 利用者視点を優先し `index -> model` を採用
- 形: `Group[index][model]`
- 公開面: `IEnumerable` を維持

4. 欠損と値 null
- 決定: 区別する
- 方式: `indexer` 維持 + `GetState(modelIndex)` 追加で 3 値識別

5. 性能上限
- 決定: 事前固定しない
- 方針: 体感性能を重視して改善
