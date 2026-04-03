# Implementation Checklist

1. `CompareResult<T>` / `CompareIssue` を実装したか
2. `Parallel<T>.GetState(modelIndex)` を実装したか
3. CompareKey 無し List を `Skip + Error` で扱うか
4. 重複キーを Error 化しているか
5. `IEnumerable<T>` を 1 回だけマテリアライズしているか
6. `SelectMany` 後も `ParallelXxx` が維持されるテストがあるか
7. strict モードで例外パスを検証したか
8. `Path/ModelIndex/KeyText` を issue に付与しているか
