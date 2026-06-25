# Sub-agent実行レポート

## タスク

- 目的: T-087 follow-up の Attribute key access test 追加を含む差分レビュー
- タスク種別: review

## sub-agentを使う理由

- 理由: review-enforcer により完了前の dedicated review が必須であり、ユーザー指定により gpt-5.5 high の sub-agent でレビューするため。

## 対象範囲

- 対象: PR #40 follow-up 差分全体、特に `Attribute["..."].Value` による E2E 検証が `AtIndex` 依存を残していないか。

## 対象外

- 対象外: 新規仕様追加、object/composite key の path 復元、dynamic projection の再設計。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,260p' reports/task-t-087-followup-dictionary-key-review-r3-20260625141638.md`
  - `git status --short`
  - `git diff --stat`
  - `git diff --check`
  - `git diff -- src/SSC.Generators/ParallelViewGenerator.cs`
  - `git diff -- src/SSC/GeneratedProjectionRuntime.cs`
  - `git diff -- src/SSC/ParallelCompareApi.cs src/SSC/ParallelNode.cs`
  - `sed -n '1,260p' reports/task-t-087-followup-dictionary-key-20260625135623.md`
  - `git diff -- tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - `git diff -- tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
  - `git diff -- doc/design/detail/01-DomainModel.md doc/design/detail/02-PublicApi.md`
  - `git diff -- tasks/phases-status.md tasks/tasks-status.md`
  - `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests|FullyQualifiedName~GeneratedProjectionE2ETests"`
    - Passed. Failed: 0, Passed: 15, Skipped: 0.
  - `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/markdown-word-checker/SKILL.md`
  - `find tools/lint -maxdepth 2 -type f -print`
    - `tools/lint` 配下の lint 設定ファイルなし。
  - `test -f package.json && sed -n '1,220p' package.json || true`
    - `package.json` は `{}`。
  - `npm run lint:md`
    - Unsupported: `Missing script: "lint:md"`。

## 対象ファイル

- 変更または確認したファイル:
  - `src/SSC.Generators/ParallelViewGenerator.cs`
  - `src/SSC/GeneratedProjectionRuntime.cs`
  - `src/SSC/ParallelCompareApi.cs`
  - `src/SSC/ParallelNode.cs`
  - `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - `tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
  - `doc/design/detail/01-DomainModel.md`
  - `doc/design/detail/02-PublicApi.md`
  - `tasks/phases-status.md`
  - `tasks/tasks-status.md`
  - `reports/task-t-087-followup-dictionary-key-20260625135623.md`
  - `reports/task-t-087-followup-dictionary-key-review-r3-20260625141638.md`

## 指摘事項

- 指摘要約または「指摘なし」: 指摘なし。
  - Attribute key access test は `root.Root.Attribute["id"].Value`、`root.Root.Attribute["source"].Value`、nested `root.Root.ChildrenOfNode[0].Attribute["name"].Value` を直接検証しており、Attribute の値アクセスは `AtIndex` 依存に戻っていない。
  - Dictionary generated access は generator が Dictionary member を `ParallelGeneratedDictionary<TKey, TElement, TView>` として生成し、runtime が compare 時の normalized `KeyValue` と `KeyComparer` で `dict[key]` lookup するため、string key 限定ではなく typed key access になっている。
  - diff path bracket discriminator access は Dictionary 側で `ByPathKey(string)` に分離され、key union 順 access は `AtIndex(int)` に分離されている。
  - key なし List は従来どおり index 順 access で、今回の Dictionary typed key indexer とは分離されている。

## 結果

- 結果: T-087 follow-up 差分はレビュー観点を満たしており、blocking finding はなし。`git diff --check` と指定 E2E は成功。

## リスク

- 未解決のリスクまたは後続対応:
  - object/composite key の path 復元と dynamic projection の再設計は対象外。
  - Markdown lint は repository 側に `lint:md` script と repo-local `tools/lint` 設定がないため unsupported。Markdown 文言の機械チェックは pass として扱っていない。
