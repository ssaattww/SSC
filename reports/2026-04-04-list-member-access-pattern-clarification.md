# 2026-04-04 List Member Access Pattern Clarification

## Summary

- ユーザー要望「Items[index] と model slot（left/right）で値を取りたい」に対して、現行 API での等価アクセスをテストと設計書へ明示。
- 参照方法を `Items[index] x modelIndex` として固定化。

## Implemented Changes

- `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
  - `Compare_WithEnumerableProperty_MaterializesEachModelExactlyOnce` に以下の検証を追加。
    - `items[0][0]?.Value == 10`
    - `items[1][0]?.Value == 20`
    - `items[0][1]?.Value == 30`
    - `items[1].GetState(1) == ValueState.Missing`
- `doc/design/detail/02-PublicApi.md`
  - `Container Member Access Pattern (Current API)` 節を追加。
  - `GetChildren<TElement>(memberName)` + `node[modelIndex]` の利用例を明記。

## Notes

- 現行ライブラリは `root.Items[0].Value[0]` のような型付きプロパティ投影 API ではなく、
  `root.GetChildren<KeyedItem>(nameof(Root.Items))[index][modelIndex]?.Value` が正式なアクセス形。

## Validation

- Command:
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "Compare_WithEnumerableProperty_MaterializesEachModelExactlyOnce|Compare_WithListMemberAcrossModels_BuildsUnionAndPreservesModelSlots"`
- Result:
  - Passed: 2, Failed: 0
