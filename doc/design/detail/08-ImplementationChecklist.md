# Implementation Checklist

## 1. API Surface

1. `Compare<T>(IReadOnlyList<T>, CompareConfiguration?)` を実装したか
2. `Parallel<T>` に `GetState(modelIndex)` を実装したか
3. `CompareResult<T>` / `CompareIssue` を実装したか

## 2. Container Rules

4. Dictionary 正規化で `TKey` を比較キーとして使っているか
5. List/Array で CompareKey 必須を検証しているか
6. CompareKey 無し List を `Skip + Error` 化しているか
7. 重複キーを Error 化しているか
8. IEnumerable の 1 回マテリアライズを保証しているか

## 3. Error/Strict Behavior

9. Strict=false で Issues 蓄積動作を確認したか
10. Strict=true で即時例外動作を確認したか
11. `Path/ModelIndex/KeyText` を必要箇所で付与しているか

## 4. SelectMany Semantics

12. SelectMany 後も `ParallelXxx` が維持されるテストがあるか
13. `element[i][0/1]` の model slot 意味を崩していないか
14. 欠損時に空列/片側展開ルールを満たすか

## 5. Determinism/Quality

15. 同一入力で列挙順が再現するか
16. 文字列キー比較が `Ordinal` で固定されているか
17. Reflection キャッシュがスレッド安全か
18. 中間データ破棄でリークを起こさないか

## 6. Minimum Test Set

19. 2 model / N model の正常系
20. 欠損混在（片側・両側）
21. CompareKey 無し / 重複キー異常系
22. Dictionary/List/IEnumerable 各コンテナ系
23. SelectMany の順序保証
