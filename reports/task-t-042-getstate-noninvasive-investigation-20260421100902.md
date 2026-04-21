# Sub-agent実行レポート

## タスク

- 目的: T-042 の実装前調査として、dynamic 値パス `GetState` が getter 実行に依存している箇所、非侵襲化の実装方針、必要テストを整理する。
- タスク種別: 調査 / 実装計画

## sub-agentを使う理由

- 理由: AGENTS.md の指示により、コード調査は codex へ委譲する必要があるため。

## 対象範囲

- 対象:
  - dynamic 値パス `GetState` の現行実装
  - 関連する内部状態表現
  - 既存テストと追加すべき回帰テスト
  - 必要なら設計書更新対象

## 対象外

- 対象外:
  - 実装適用そのもの
  - commit / PR 作成
  - unrelated な README/設計リファクタ

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `rg -n "GetState|value-path|dynamic value|getter" /home/ibis/dotnet_ws/SSC -g '!**/bin/**' -g '!**/obj/**'`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/src/SSC/ParallelDynamicAccessExtensions.cs`
- `sed -n '1,320p' /home/ibis/dotnet_ws/SSC/src/SSC/GeneratedProjectionRuntime.cs`
- `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/src/SSC/ParallelNode.cs`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/src/SSC.Generators/ParallelViewGenerator.cs`
- `rg -n "SetChildren\\(|Children\\(|GetPresenceState\\(|GetValue\\(" /home/ibis/dotnet_ws/SSC/src/SSC -g '!**/bin/**' -g '!**/obj/**'`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/src/SSC/ParallelCompareApi.cs`
- `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/src/SSC/NodePresenceState.cs`
- `sed -n '1,280p' /home/ibis/dotnet_ws/SSC/tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
- `sed -n '1,240p' /home/ibis/dotnet_ws/SSC/tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
- `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/src/SSC/Contracts.cs`
- `sed -n '1,220p' /home/ibis/dotnet_ws/SSC/doc/design/detail/09-ValueStateBehavior.md`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/doc/design/detail/02-PublicApi.md`
- `sed -n '1,120p' /home/ibis/dotnet_ws/SSC/doc/design/detail/08-ImplementationChecklist.md`
- `sed -n '1,120p' /home/ibis/dotnet_ws/SSC/README.md`
- `sed -n '225,265p' /home/ibis/dotnet_ws/SSC/README.md`
- `sed -n '1,180p' /home/ibis/dotnet_ws/SSC/reports/2026-04-04-typed-access-approach-evaluation.md`
- `nl -ba /home/ibis/dotnet_ws/SSC/src/SSC/ParallelDynamicAccessExtensions.cs | sed -n '150,320p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/src/SSC/GeneratedProjectionRuntime.cs | sed -n '150,270p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/src/SSC/ParallelCompareApi.cs | sed -n '70,220p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs | sed -n '120,220p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/doc/design/detail/02-PublicApi.md | sed -n '160,260p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/doc/design/detail/09-ValueStateBehavior.md | sed -n '1,120p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/README.md | sed -n '100,260p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/src/SSC/ValueStateExtensions.cs | sed -n '1,120p'`
- `nl -ba /home/ibis/dotnet_ws/SSC/doc/design/detail/08-ImplementationChecklist.md | sed -n '1,120p'`

## 対象ファイル

- 確認したファイル:
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `src/SSC/GeneratedProjectionRuntime.cs`
  - `src/SSC/ParallelCompareApi.cs`
  - `src/SSC/ParallelNode.cs`
  - `src/SSC/NodePresenceState.cs`
  - `src/SSC/Contracts.cs`
  - `src/SSC/ValueStateExtensions.cs`
  - `src/SSC.Generators/ParallelViewGenerator.cs`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
  - `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - `doc/design/detail/02-PublicApi.md`
  - `doc/design/detail/08-ImplementationChecklist.md`
  - `doc/design/detail/09-ValueStateBehavior.md`
  - `README.md`
  - `reports/2026-04-04-typed-access-approach-evaluation.md`
- 変更候補:
  - `src/SSC/Contracts.cs`
  - `src/SSC/ParallelNode.cs`
  - `src/SSC/ParallelCompareApi.cs`
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `src/SSC/GeneratedProjectionRuntime.cs` もしくは `src/SSC.Generators/ParallelViewGenerator.cs`（generated parity を取る場合）
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`（generated parity を取る場合）
  - `tests/SSC.E2E.Tests/CompareApiE2ETests.cs` もしくは新規の dynamic 専用 E2E ファイル
  - `doc/design/detail/02-PublicApi.md`
  - `doc/design/detail/09-ValueStateBehavior.md`
  - `README.md`

## 指摘事項

- 指摘要約:
  1. `DynamicParallelValuePathView.GetState` は `ResolveValue` を経由し、各モデル slot ごとに `GetValue(modelIndex)` と `property.GetValue(current)` を実行しているため、getter 実行に直接依存している。根拠は `src/SSC/ParallelDynamicAccessExtensions.cs:188-297`。
  2. 現在の compare 木は `ParallelCompareApi.BuildNodeGeneric` で container 系だけを child node 化しており、scalar プロパティはノードとして保持していない。したがって dynamic 値パス側に getter なしで参照できる内部状態がまだ無い。根拠は `src/SSC/ParallelCompareApi.cs:88-125` と `src/SSC/ParallelNode.cs:1-135`。
  3. `ParallelGeneratedValue.GetState` も同じ `ResolveValue` / getter 依存パターンを使っているため、dynamic だけ直すと generated と挙動差が残る。根拠は `src/SSC/GeneratedProjectionRuntime.cs:157-259`。
  4. 設計文書は generated / dynamic の value path に同一の `GetState` 意味を置いており、今回の非侵襲化は文書更新対象になり得る。根拠は `doc/design/detail/02-PublicApi.md:169-218`、`doc/design/detail/09-ValueStateBehavior.md:36-67`、`README.md:169-218`。

## 結果

- 結果:
  - 最小で安全な実装方針は、scalar プロパティも compare 時に内部ノードとして materialize し、dynamic value path の `GetState` はその内部ノードの `NodePresenceState` / 値へ辿る形に変える案だった。
  - 具体的には、`DynamicParallelValuePathView` の reflection ベース `ResolveValue` をやめ、現在位置の `IParallelNode` を持ち回る構造に変えるのがよい。これで `GetState` から getter 実行を外せる。
  - 変更順は TDD 先行がよい。先に side effect getter / exception getter の回帰テストを追加し、その後に compare 側へ scalar node materialization を入れる。
  - generated との意味一致を維持するなら、`GeneratedProjectionRuntime` も同じ内部ノード参照へ寄せるのが推奨。dynamic のみで止めると文書整合と回帰範囲が分かれやすい。
  - 実装候補の優先度は以下。
    1. `Contracts.cs` に scalar member 参照用の内部 API を追加
    2. `ParallelNode.cs` に scalar member の状態/子ノード保持を追加
    3. `ParallelCompareApi.cs` で scalar property を build 時に materialize
    4. `ParallelDynamicAccessExtensions.cs` で reflection 追跡を廃止
    5. 必要なら generated 側も同じ経路へ寄せる

  - 先に書くべきテストは以下。
    1. side effect getter で `GetState` 呼び出し後に getter 呼び出し回数が増えないこと
    2. exception getter でも `GetState` 時に例外が再発しないこと
    3. nested value path でも `Matched / Mismatched / Missing` が維持されること
    4. container access と node-level `GetState` が既存回帰しないこと

  - 推奨判断:
    - `dynamic` だけを局所修正する案より、compare 木に scalar member state を持たせる案を推奨する。getter 依存を確実に外せるため。
    - ただし、scalar getter の評価タイミングが compare 時に前倒しされる可能性があるため、その挙動差はテストと README で明示する必要がある。

## リスク

- 未解決のリスクまたは後続対応:
  - scalar getter の副作用や例外が `GetState` ではなく compare/build 時に前倒しで表面化する可能性がある。
  - compare 木の materialization 範囲が増えるため、対象型が大きい場合のメモリ/初期化コストを確認する必要がある。
  - generated projection を同じ内部ノード経路へ寄せない場合、dynamic と generated で `GetState` の実装差が残る。
  - 既存の `README.md` と design docs は `dynamic` / `generated` の同一挙動を前提に読めるため、実装後に説明文の見直しが必要になる。
