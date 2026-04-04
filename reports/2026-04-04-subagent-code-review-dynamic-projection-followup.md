# 2026-04-04 Subagent Code Review: Dynamic Projection Follow-up

## Scope

- Current branch diff vs `main`, with focus on dynamic projection changes:
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `src/SSC/ParallelNode.cs`
  - `src/SSC/Contracts.cs`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `doc/design/detail/01-DomainModel.md`
  - `doc/design/detail/02-PublicApi.md`
- Subagent runs:
  - Run 1: Descartes
  - Run 2: Aquinas（本レポートに統合）

## Findings (Consolidated)

### High

1) `AsDynamic` の受け口型が `CompareResult<T>.Root` と不整合
- Files:
  - `src/SSC/ParallelDynamicAccessExtensions.cs` (extension receiver)
  - `src/SSC/Contracts.cs` (`CompareResult<T>.Root` type)
  - `doc/design/detail/01-DomainModel.md`
  - `doc/design/detail/02-PublicApi.md`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`（現状はキャストで回避）
- Detail:
  - 設計書サンプルは `result.Root.AsDynamic()` を前提に読めるが、実装の extension 受け口と一致せずそのままではコンパイルできない経路がある。
- Impact:
  - 公開 API 契約と利用サンプルの不一致。
- Suggested Fix:
  - `Root` から直接使える extension へ統一するか、設計書を実装契約に合わせて明確化。

2) dynamic 値パスでの `GetState` が対象メンバー状態を返さない可能性
- File:
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
- Detail:
  - パス追跡と `GetState` 解決の整合が弱く、`root....Foo.GetState(i)` が親ノード側の状態を返し得る。
- Impact:
  - `PresentNull` / `Missing` 判定が意図したメンバー単位にならないリスク。
- Suggested Fix:
  - `GetState` を path-aware にして、最終参照先の node/value へ評価を揃える。

### Medium

3) dynamic 予約名衝突（`Count` / `KeyText`）
- File:
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
- Detail:
  - dynamic view が `Count` / `KeyText` を先に返すため、モデル同名プロパティと衝突する。
- Impact:
  - モデル設計次第で値アクセス不能または誤読。
- Suggested Fix:
  - メンバー解決順またはメタ名を見直す。

4) dynamic list 範囲外アクセスの例外契約未統一
- File:
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
- Detail:
  - 範囲外アクセスで生の `ArgumentOutOfRangeException` が漏れる。
- Impact:
  - 既存 API の例外契約と一貫しない。
- Suggested Fix:
  - dynamic 側で境界チェックし、公開契約として統一。

5) `ParallelNode.CreateLeaf(values, states)` 長さ不一致未検証
- File:
  - `src/SSC/ParallelNode.cs`
- Detail:
  - 引数長不一致でも生成でき、後段で遅延不整合が起きる。
- Impact:
  - 不正インスタンス生成リスク。
- Suggested Fix:
  - 生成時の即時検証を追加。

6) dynamic 投影の回帰テスト不足
- File:
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
- Detail:
  - ハッピーパス中心で、High/Medium 領域の再現テストが不足。
- Suggested Additions:
  - `Root` からの直接 dynamic 入口
  - 値パス `GetState` の `PresentNull` / `Missing`
  - `Count` / `KeyText` 衝突
  - list 範囲外 index の例外契約

### Low

7) 設計書の内部表現型が実装と不一致
- File:
  - `doc/design/detail/01-DomainModel.md`
- Detail:
  - 記載は `Dictionary<string, IParallelNode>` だが、実装は `Dictionary<string, IReadOnlyList<IParallelNode>>`。
- Impact:
  - 構造理解の齟齬。
- Suggested Fix:
  - 設計書型を実装へ合わせる。

## Status

- This file consolidates two subagent review passes for the same dynamic projection feature.
- Implementation fixes are tracked separately and not included in this review task.
