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
16. 文字列キー比較が `StringKeyComparison` 設定（`Ordinal` / `OrdinalIgnoreCase`）に従うか
17. `OrdinalIgnoreCase` 時の `KeyText` が Ordinal 最小表記に正規化されるか
18. Reflection キャッシュがスレッド安全か
19. 中間データ破棄でリークを起こさないか

## 6. Minimum Test Set

20. 2 model / N model の正常系
21. 欠損混在（片側・両側）
22. CompareKey 無し / 重複キー異常系
23. Dictionary/List/IEnumerable 各コンテナ系
24. SelectMany の順序保証

## 7. Test Strategy (TDD + E2E)

25. 仕様項目ごとに先に失敗テストを書く（TDD）
26. API 入口から出口までの E2E テストを優先する
27. E2E で `Path/IssueCode/model slot` をまとめて検証する
28. unit テストは E2E で不足する境界条件を補完する

## 8. Generated Projection

29. `GenerateParallelViewAttribute` を追加し、生成対象の明示契約を定義したか
30. Source Generator で `AsGeneratedView()` と型付き view を生成できるか
31. generated API で `list index -> model index` アクセスを再現できるか
32. generated API の範囲外 index が `ModelIndexOutOfRange` で失敗するか
33. `AsDynamic()` の既存挙動が回帰していないか
34. 複数 namespace / nested type / internal type で生成コードがコンパイルできるか
35. `Count/KeyText` 衝突系と nullable value-path 系で dynamic 回帰がないか
