# Sub-agent実行レポート

## タスク

- 目的: T-042 の TDD として、dynamic 値パス `GetState` の非侵襲化を要求する failing test を追加し、失敗を実証する。
- タスク種別: テスト作成 / 失敗確認

## sub-agentを使う理由

- 理由: テスト作成と test 実行証跡は workflow 上 sub-agent で行う必要があるため。

## 対象範囲

- 対象:
  - dynamic 値パス `GetState` の side-effect getter / exception getter 回帰テスト
  - 必要最小限の nested value-path 回帰テスト
  - failing proof としての対象テスト実行

## 対象外

- 対象外:
  - production code 修正
  - README / 設計書更新
  - commit / PR 作成

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/development-orchestrator/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/reports/task-t-042-getstate-noninvasive-investigation-20260421100902.md`
  - `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/reports/task-t-042-implementation-plan-review-20260421101328.md`
  - `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/reports/task-t-042-design-update-20260421102220.md`
  - `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/doc/design/detail/02-PublicApi.md`
  - `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/doc/design/detail/06-ExecutionPipeline.md`
  - `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/doc/design/detail/09-ValueStateBehavior.md`
  - `rg -n "GetState|Dynamic|dynamic|ValueState|Select\\(|getter|MetricA|Detail\\.Label|ParallelDynamic" tests/SSC.E2E.Tests tests/SSC.Unit.Tests -g '!**/bin/**' -g '!**/obj/**'`
  - `sed -n '150,340p' tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `sed -n '340,760p' tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `nl -ba src/SSC/ParallelDynamicAccessExtensions.cs | sed -n '180,330p'`
  - `git diff -- tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --filter "FullyQualifiedName~Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall|FullyQualifiedName~Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException"`
  - `nl -ba tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs | sed -n '240,330p'`
  - `git status --short -- tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs reports/task-t-042-tdd-failing-tests-20260421102611.md`

## 対象ファイル

- 変更または確認したファイル:
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `reports/task-t-042-tdd-failing-tests-20260421102611.md`
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `doc/design/detail/02-PublicApi.md`
  - `doc/design/detail/06-ExecutionPipeline.md`
  - `doc/design/detail/09-ValueStateBehavior.md`
  - `reports/task-t-042-getstate-noninvasive-investigation-20260421100902.md`
  - `reports/task-t-042-implementation-plan-review-20260421101328.md`
  - `reports/task-t-042-design-update-20260421102220.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  1. 現行 `DynamicParallelValuePathView.GetState` は `ResolveValue(...)` 内で `property.GetValue(current)` を辿っており、dynamic nested value path の state lookup 自体が getter 実行に依存している。根拠: `src/SSC/ParallelDynamicAccessExtensions.cs:188-297`
  2. 追加した `Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall` は、`GetState` 前の getter 呼び出し回数 `0` に対して、実行後に `2` へ増加することを検出して失敗した。`GetState` が選択 slot と比較対象 slot の両方で getter を再実行している証跡になっている。
  3. 追加した `Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException` は、`GetState` 呼び出し時に `TargetInvocationException`（inner: `InvalidOperationException: boom-left`）が再送出されることを検出して失敗した。例外 getter が compare 側で吸収されず、state lookup 中に再評価されている証跡になっている。
  4. nested value-path の intended contract は既存 `Compare_DynamicProjection_ValuePathGetState_ReflectsMemberState` に加え、今回の side-effect test でも `ValueState.Mismatched` を明示的に assert して維持した。最小変更を優先し、追加の第3テストは作成していない。

## 結果

- 結果:
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs` に failing test を 2 件追加した。
    - `Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall`
    - `Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException`
  - production code は変更していない。
  - 対象テストだけを `dotnet test` の filter で実行し、2 件とも失敗することを確認した。
  - 失敗要約:
    - side-effect getter: `Assert.Equal()` 失敗。期待 `0`、実際 `2`
    - exception getter: `Assert.Null()` 失敗。`GetState` 中に `TargetInvocationException` を再送出
  - failing-proof 対象テスト名:
    - `SSC.E2E.Tests.ContainerAndSelectManyE2ETests.Compare_DynamicProjection_NestedValuePathGetState_DoesNotInvokeGetterDuringCall`
    - `SSC.E2E.Tests.ContainerAndSelectManyE2ETests.Compare_DynamicProjection_NestedValuePathGetState_DoesNotRethrowGetterException`

## リスク

- 未解決のリスクまたは後続対応:
  - テストは意図どおり failing のままなので、T-042 実装では compare/build 側で member state を materialize し、dynamic value-path `GetState` が getter を再実行しない経路へ差し替える必要がある。
  - getter 例外時の最終 `ValueState` と issue 記録内容は今回の failing test では固定していない。後続実装で compare-time issue policy に合わせて追加 assertion を検討できる。
