# Sub-agent実行レポート

## タスク

- 目的: T-042 の実装計画を上位モデルで再評価し、採用方針・設計影響・TDD 順序を確定する。
- タスク種別: 計画レビュー / 設計判断

## sub-agentを使う理由

- 理由: 実装計画のような重要判断は独立した `codex` pass で確認する必要があるため。`codex exec` による sub-agent 起動を試したが、この実行環境では session 書き込み制約と localhost websocket 接続制約で実行不能だったため、失敗証跡を残したうえで親がコード根拠を再確認して判断をまとめた。

## 対象範囲

- 対象:
  - 既存の調査レポートの妥当性確認
  - 実装方針の選択肢比較
  - 設計ドキュメント更新要否の判断
  - TDD の先行テスト計画整理

## 対象外

- 対象外:
  - 実装適用そのもの
  - `tasks/` / `phases/` / `feedback` ファイル更新
  - commit / PR 作成
  - generated API 全面 redesign の実施

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/reports/task-t-042-getstate-noninvasive-investigation-20260421100902.md`
- `sed -n '1,260p' /home/ibis/dotnet_ws/SSC/reports/task-t-042-implementation-plan-review-20260421101328.md`
- `rg -n "T-042|GetState|dynamic value path|non-invasive|noninvasive" /home/ibis/dotnet_ws/SSC -g '!bin' -g '!obj'`
- `sed -n '1,260p' src/SSC/ParallelDynamicAccessExtensions.cs`
- `sed -n '1,260p' src/SSC/GeneratedProjectionRuntime.cs`
- `sed -n '1,260p' src/SSC/ParallelCompareApi.cs`
- `sed -n '1,220p' src/SSC/ParallelNode.cs`
- `sed -n '1,220p' src/SSC/Contracts.cs`
- `sed -n '150,290p' tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
- `sed -n '60,180p' tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
- `sed -n '150,280p' doc/design/detail/02-PublicApi.md`
- `sed -n '1,140p' doc/design/detail/09-ValueStateBehavior.md`
- `sed -n '1,120p' doc/design/detail/06-ExecutionPipeline.md`
- `sed -n '1,140p' reports/2026-04-04-typed-access-approach-evaluation.md`
- `sed -n '1,120p' reports/2026-04-04-source-generator-roadmap.md`
- `sed -n '1,120p' reports/chat-handover-for-new-thread-20260404_144004.md`
- `nl -ba src/SSC/ParallelDynamicAccessExtensions.cs | sed -n '150,330p'`
- `nl -ba src/SSC/GeneratedProjectionRuntime.cs | sed -n '150,280p'`
- `nl -ba src/SSC/ParallelCompareApi.cs | sed -n '70,140p'`
- `nl -ba src/SSC/ParallelNode.cs | sed -n '1,170p'`
- `nl -ba src/SSC.Generators/ParallelViewGenerator.cs | sed -n '150,180p'`
- `nl -ba tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs | sed -n '188,240p'`
- `nl -ba doc/design/detail/02-PublicApi.md | sed -n '180,240p'`
- `nl -ba doc/design/detail/09-ValueStateBehavior.md | sed -n '24,70p'`
- `nl -ba doc/design/detail/06-ExecutionPipeline.md | sed -n '15,80p'`
- `codex --help`
- `codex exec --help`
- `cat <<\"EOF\" | codex exec --ephemeral -C /home/ibis/dotnet_ws/SSC --sandbox workspace-write -m gpt-5.4 -c model_reasoning_effort=\"high\" -`
  - 結果: `~/.codex/sessions` への書き込みで失敗
- `mkdir -p /tmp/codex-home/.codex/... && HOME=/tmp/codex-home codex exec --ephemeral -C /home/ibis/dotnet_ws/SSC --sandbox workspace-write -m gpt-5.4 -c model_reasoning_effort=\"high\" \"Reply with exactly: ok\"`
  - 結果: localhost websocket (`127.0.0.1:2455`) への接続が `Operation not permitted` で失敗

## 対象ファイル

- 確認したファイル:
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `src/SSC/GeneratedProjectionRuntime.cs`
  - `src/SSC/ParallelCompareApi.cs`
  - `src/SSC/ParallelNode.cs`
  - `src/SSC/Contracts.cs`
  - `src/SSC.Generators/ParallelViewGenerator.cs`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - `doc/design/detail/02-PublicApi.md`
  - `doc/design/detail/06-ExecutionPipeline.md`
  - `doc/design/detail/09-ValueStateBehavior.md`
  - `tasks/tasks-status.md`
  - `reports/task-t-042-getstate-noninvasive-investigation-20260421100902.md`
  - `reports/2026-04-04-typed-access-approach-evaluation.md`
  - `reports/2026-04-04-source-generator-roadmap.md`
  - `reports/chat-handover-for-new-thread-20260404_144004.md`
- 実装時の主な変更候補:
  - `src/SSC/Contracts.cs`
  - `src/SSC/ParallelNode.cs`
  - `src/SSC/ParallelCompareApi.cs`
  - `src/SSC/ParallelDynamicAccessExtensions.cs`
  - `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
  - `doc/design/detail/02-PublicApi.md`
  - `doc/design/detail/06-ExecutionPipeline.md`
  - `doc/design/detail/09-ValueStateBehavior.md`
  - 必要なら `README.md`

## 指摘事項

- 指摘要約:
  1. 既存調査レポートの中核診断には賛成。`DynamicParallelValuePathView.GetState` は `ResolveValue` 経由で `property.GetValue(current)` を都度実行しており、`GetState` 自体が getter 実行に依存している。`src/SSC/ParallelDynamicAccessExtensions.cs:188-297`
  2. compare 木に scalar / object member 用の内部ノードが無い、という指摘も正しい。現行 `BuildNodeGeneric` は container だけを `SetChildren` しており、`ParallelNode<T>` 側にも member node 保持 API が存在しない。`src/SSC/ParallelCompareApi.cs:88-123`, `src/SSC/ParallelNode.cs:7-8`, `src/SSC/ParallelNode.cs:113-137`
  3. ただし、前レポートの「generated も同じ task で同経路へ寄せる」提案には反対。generated view は各 property を `new(_node, static model => getterExpression)` で生成し、nested path は `Select(Func<TValue, TNext>)` で member 名情報を失うため、dynamic と同じ non-invasive 経路へ即時統合すると T-042 の設計面が広がりすぎる。`src/SSC.Generators/ParallelViewGenerator.cs:164-167`, `src/SSC/GeneratedProjectionRuntime.cs:217-232`, `doc/design/detail/02-PublicApi.md:189-229`
  4. T-042 の第一実装として推奨するのは、dynamic 向けに comparable non-container member を compare/build 時に内部 `IParallelNode` として materialize し、dynamic member access をその node 参照へ切り替える案である。これなら `GetState` は既存 `ParallelNode<T>.GetState` を再利用でき、T-041 で確定した「最終参照先の状態を返す」契約も維持しやすい。`src/SSC/ParallelNode.cs:57-99`, `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs:191-239`
  5. 設計影響は「なし」ではない。Execution Pipeline は現在 container 正規化中心で、scalar member materialization を明示していない。さらに Public API / ValueState 文書は generated と dynamic の value path を同一説明にしているため、T-042 の実装範囲を dynamic に限定するか、あるいは getter 評価タイミングの差を文書化する必要がある。`doc/design/detail/06-ExecutionPipeline.md:37-55`, `doc/design/detail/02-PublicApi.md:213-229`, `doc/design/detail/09-ValueStateBehavior.md:36-41`

## 結果

- 結果:
  - 前レポートに対する判断:
    - 賛成: dynamic `GetState` を reflection path から切り離すには compare 時点で参照可能な内部状態が必要、という主張。
    - 反対: generated parity まで同一 task で抱え込む提案。`Select(Func<...>)` 形状のため、同時実施は bounded ではない。
  - 推奨実装方針:
    1. `design-doc-maintainer` 判断として、実装前に設計文書を更新する。対象は少なくとも `doc/design/detail/02-PublicApi.md`, `doc/design/detail/06-ExecutionPipeline.md`, `doc/design/detail/09-ValueStateBehavior.md`。
    2. compare/build で container 以外の comparable member も slot 配列へ落とし込み、`ParallelNode<T>` が内部 member node を保持できるようにする。公開 API は増やさず、`IParallelNodeInternal` に `TryGetMemberNode(...)` 相当の internal アクセスを追加する方針が最も収まりがよい。
    3. `ParallelDynamicAccessExtensions.cs` は member 名の reflection 追跡をやめ、node view から member node view へ辿る形に置き換える。可能なら `DynamicParallelValuePathView` 自体を縮小または廃止し、scalar/object member も node ベースで扱う。
    4. getter 例外は container property と同じく compare 時に `CompareContext.RecordExecutionError(...)` へ寄せ、対象 slot を `Missing` 扱いに落とす設計を第一候補とする。これで「`GetState` 呼び出し時に例外が再発しない」を満たしやすい。`src/SSC/ParallelCompareApi.cs:156-160`, `src/SSC/ParallelCompareApi.cs:572-580`
    5. generated runtime は T-042 の完了条件から外し、別タスクまたは設計追補として扱う。必要なら T-042 実装後に「generated nested value path を non-invasive にするには API を `Func` から member-aware 形へ見直す必要がある」と記録する。
  - 代替案比較:
    1. dynamic wrapper だけに path-state cache を持つ案:
       - 変更箇所は見かけ上少ないが、nested path / null / issue handling を独自実装することになり、`ParallelNode<T>.GetState` とロジックが二重化するため非推奨。
    2. dynamic + generated を同時に member-node 化する案:
       - 長期整合性は高いが、generated `Select(Func<...>)` が path identity を失うため、T-042 の bounded scope を超えやすい。
    3. dynamic 向け member-node materialization 案:
       - 既存 compare 木と `ParallelNode<T>.GetState` を再利用でき、T-042 の exit criteria に最も直接効くため推奨。
  - 推奨する実装順序:
    1. 設計更新: scope と getter 評価タイミングを明文化
    2. TDD: dynamic 向け failing test を追加
    3. compare/build と node 内部 API を実装
    4. dynamic access 実装を node traversal へ置換
    5. targeted test 実行後、full test へ進む
  - TDD 先行テスト計画:
    1. `Compare_DynamicProjection_GetState_DoesNotInvokeGetterDuringCall`
       - side-effect getter を持つモデルを用意し、`Compare(...)` 完了時点の呼び出し回数を記録したうえで、`root....Value.GetState(modelIndex)` 後に増分が 0 であることを確認する。
    2. `Compare_DynamicProjection_GetState_DoesNotRethrowGetterException`
       - getter が例外を投げるモデルで compare を実行し、issue 記録方針に従って `GetState` 呼び出し時に追加例外が出ないことを確認する。
    3. `Compare_DynamicProjection_NestedValuePathGetState_StillReflectsFinalMemberState`
       - 既存 `Detail.Label` 系ケースで `Missing / Matched / Mismatched` が維持されることを確認する。
    4. `Compare_DynamicProjection_ValueIndexAccess_RemainsCompatible`
       - `root....MetricA[modelIndex]` と `root....Detail.Label[modelIndex]` の値取得が既存契約どおり動くことを確認する。
    5. `ParallelNode_InternalMemberNodeAccess_ReturnsExpectedState`（必要なら Unit）
       - member node 経由で presence/state が親ロジックと整合することを、小さい fixture で確認する。

## リスク

- 未解決のリスクまたは後続対応:
  - comparable scalar/object getter の評価タイミングが compare/build 側へ前倒しされるため、副作用が「`GetState` 呼び出し時」から「compare 時」に移る。この差分は設計書と README で明記が必要。
  - getter 例外を `Missing + issue` として扱うか、strict 時のみ例外とするかを先に決めないと、テスト期待値がぶれる。
  - member node を全 comparable property に持たせると、メモリ使用量と build 時反射回数は増える。T-042 では correctness 優先でよいが、後続で測定余地がある。
  - generated nested value path の non-invasive 化は別設計論点として残る。`Func` ベース `Select(...)` のままでは path-aware 化が難しい。
  - sub-agent による独立再評価は環境制約で実行できなかったため、必要なら権限制約のない run で再度 independent pass を取る余地がある。
