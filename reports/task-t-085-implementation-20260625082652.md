# Sub-agent実行レポート

## タスク

T-085 gist `XmlCustom` 同等 E2E 比較の修正。

## sub-agentを使う理由

ユーザー指示により実装を sub-agent に委譲するため。TDD で gist `XmlCustom.cs` と同等の E2E を追加し、失敗確認後に最小修正で比較成功まで進める。

## 対象範囲

- gist: https://gist.github.com/ssaattww/fbd4fa683d03cd03dd73df0fa37bf1a0
- `tests/SSC.E2E.Tests/`
- `tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj`
- `src/SSC/`
- `src/SSC.Generators/`
- 必要な場合のみ `doc/design/detail/02-PublicApi.md`
- 必要な場合のみ `Design/BreakingChanges.md`

## 対象外

- `tasks/tasks-status.md` と `tasks/phases-status.md` の編集
- T-085 と無関係な既存 warning の修正
- 既存 sample の書き換え
- nested Codex / agent-spawning の実行
- report 形式の変更

## 実行コマンド

- `curl -L https://gist.githubusercontent.com/ssaattww/fbd4fa683d03cd03dd73df0fa37bf1a0/raw/ -o /tmp/t085-gist.txt`
- 失敗確認: `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests"`
  - production 修正前に失敗。
  - 失敗内容: `CompareKeyNotFoundOnSequenceElement path=Document.Root.ChildrenOfNode` と `CompareKeyNotFoundOnSequenceElement path=Document.Root.Children` により `HasError == true`。
- 修正後 focused: `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests"`
  - Passed: 1
- 修正後 focused: `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests|FullyQualifiedName~CompareApiE2ETests.Compare_WhenSequenceElementHasNoCompareKey_AlignsByOrdinalIndex|FullyQualifiedName~CompareApiE2ETests.Compare_WhenMissingCompareKeyPolicySkips_RecordsErrorAndSkips|FullyQualifiedName~ContainerAndSelectManyE2ETests.Compare_DynamicProjection_RuntimeDerivedContainerMember_WithoutCompareKeyAlignsByOrdinalIndex|FullyQualifiedName~GeneratedProjection"`
  - Passed: 15
- generated projection focused: `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjection"`
  - Passed: 11
- 全体: `dotnet test SSC.sln --configuration Release`
  - Passed: E2E 66, Unit 29
- `dotnet format SSC.sln --verify-no-changes`
  - Passed
- `git diff --check`
  - Passed
- `cat package.json`
  - Markdown lint script なしを確認

## 対象ファイル

- `tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
- `tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj`
- `tests/SSC.E2E.Tests/CompareApiE2ETests.cs`
- `tests/SSC.E2E.Tests/ContainerAndSelectManyE2ETests.cs`
- `src/SSC/CompareConfiguration.cs`
- `src/SSC/ParallelCompareApi.cs`
- `doc/design/detail/02-PublicApi.md`
- `doc/design/detail/03-ContainerRules.md`
- `doc/design/detail/05-ResultAndErrors.md`
- `Design/BreakingChanges.md`

## 指摘事項

- ユーザー割り込みで要件が明確化された。`List` / sequence 要素型に `[CompareKey]` が無い場合は `CompareKeyNotFoundOnSequenceElement` を記録して skip するのではなく、ordinal index で比較する。
- 既存 keyed sequence の key union 動作は維持する必要がある。
- `tasks/tasks-status.md` と `tasks/phases-status.md` は親管理のため編集対象外。作業開始時点から未コミット変更があったが、本作業では触っていない。

## 結果

- gist `XmlCustom.cs` 同等の E2E を追加し、`Sprache` で 2 つの XML-like 文字列を `Document` に parse して `ParallelCompareApi.Compare` するテストを追加した。
- production 修正前は `Node.Children` / `Node.ChildrenOfNode` の `[CompareKey]` 欠如により focused E2E が失敗することを確認した。
- `MissingCompareKeyListPolicy` の既定値を `AlignByIndex` に変更し、`[CompareKey]` が無い sequence は ordinal index を `KeyText` として children を構築するようにした。
- keyless sequence の value mismatch と片側 trailing extra element の Missing を回帰テストで確認した。
- dynamic runtime-derived container の keyless element も ordinal index で辿れることを回帰テストで確認した。
- `MissingCompareKeyListPolicy.SkipAndRecordError` を明示した場合の旧 skip+error 経路も回帰テストで確認した。
- public behavior 変更として design docs と breaking changes を更新した。
- 親側で review-readiness 調整として、T-085 で追加・変更した `[Fact]` 4 件に XML summary を追加した。
- 親側で focused / full validation を再実行し、worker 報告と同じ成功結果を確認した。

## リスク

- `MissingCompareKeyListPolicy` の既定値変更は public runtime behavior の変更。旧来の skip+error 前提の利用者には影響があるため `Design/BreakingChanges.md` に記録済み。
- Markdown lint script は `package.json` に存在しなかったため未実行。
- focused test 実行時に既存 `ContainerAndSelectManyE2ETests.cs(34,47)` の nullable warning `CS8603` が出ることがあるが、T-085 とは無関係な既存 warning として未対応。
