# Issue #54 コードカバレッジ可視化 実装レポート

## メタデータ

- Repository: `ssaattww/SSC`
- Issue: #54 `コードカバレッジをCIで視覚化する`
- Pull Request: #55 `Add CI code coverage visualization`
- Branch: `feature/issue-54-code-coverage`
- Base: `main` / `4f4e207c737db67ecfd37c1ee36be2816458daf5`
- 検証対象implementation HEAD: `32505d049e3db80c3fd34581fab33c4033572e48`
- 実装差分: baseより5 commits ahead
- 実装方式: userの明示指示によりRed-first TDDを省略し、workflow実装後に契約testを追加・更新
- Merge: 実施しない

## 目的

Unit TestsとE2E Testsの実行時にSSC runtime assemblyのコードカバレッジを収集し、次の2段階で確認できるようにしました。

1. GitHub Actions Job Summaryでline coverage、branch coverage、class別coverageを確認する
2. 診断artifact内のHTML reportでclass、method、source行、branch単位の未通過箇所を確認する

coverage率はテスト品質そのものを保証しないため、閾値gate、永続badge、外部SaaS、履歴蓄積は対象外です。

## 変更内容

### `.github/workflows/pr-xunit-tests.yml`

- test projectごとに`dotnet test --collect:"XPlat Code Coverage"`を実行
- TRXとcoverage attachmentが衝突しないよう、test projectごとのresults directoryを使用
- VSTestが同じCoberturaを複数箇所へ複製した場合、SHA-256で重複排除してraw coverageへ保存
- Unit TestsとE2E Testsのraw CoberturaをReportGenerator `5.5.10`で統合
- `Html`、`Cobertura`、`MarkdownSummaryGithub`、`TextSummary`を生成
- Actions Job SummaryへPR head SHAとcoverage summaryを出力
- test成功時にraw coverageが存在しない場合はworkflowを失敗させる
- test失敗時にも生成済みcoverage、ReportGeneratorログ、TRX、stdout、stderr、診断ログを可能な限りartifactへ保存
- 既存の`artifacts/test-results` upload pathと7日間retentionを維持

### `tests/SSC.Unit.Tests/Issue54CodeCoverageWorkflowContractUnitTests.cs`

次のworkflow契約を検証します。

- XPlat Code Coverage収集
- raw Cobertura保存
- `always()`条件での統合report生成
- ReportGenerator version固定
- HTML、merged Cobertura、GitHub Markdown、text summary生成
- `SSC`および`SSC.Generators` assembly filter
- ReportGenerator stdout/stderr保存
- coverage input一覧保存
- PR head SHAとActions Summary出力
- test成功時のcoverage欠落を失敗扱いすること

### `tests/SSC.Unit.Tests/GitHubActionsTestArtifactContractUnitTests.cs`

既存の診断artifact契約を、test projectごとのresults directoryへ整合させました。TRX、test stdout/stderr、generator logs、診断情報、source archive、upload条件の既存契約は維持しています。

### `doc/design/detail/13-CodeCoverageVisualization.md`

次を記録しました。

- CI生成物とartifact内path
- PRでの確認手順
- 未通過method・source行・branchの確認方法
- ローカルでcoverage reportを生成するコマンド
- `SSC.Generators`が通常のtesthost coverageに現れない場合の扱い
- coverageをテスト品質そのものと扱わない注意事項

## CI失敗と修正

### Run `30721697713` / HEAD `5c0e5e710153a2e7f90e2b67803cb29454183de4`

- Conclusion: failure
- E2E Tests: 88/88 passed
- Unit Tests: 99/100 passed
- ReportGenerator: succeeded
- Artifact: `8825063372`
- 原因: 既存契約testが`mkdir -p "$results_dir/logs"`の完全一致を要求しており、複数directoryを1 commandで作る変更と不整合
- 修正: 既存logs directory作成commandを維持

### Run `30721792856` / HEAD `f3576c17c5e6afb91c0fcab697c3faf5c3ca675b`

- Conclusion: failure
- E2E Tests: 88/88 passed
- Unit Tests: 99/100 passed
- ReportGenerator: succeeded
- Artifact: `8825091007`
- 原因: 既存契約testが旧`--results-directory "$results_dir"`を要求しており、project別results directoryへの変更と不整合
- 修正: 契約testを`project_results_dir`へ整合

失敗runのcoverage生成とartifact uploadは成功していましたが、別SHAのrunを最終判定には使用していません。

## 最終検証

### Matching-HEAD CI

- PR current implementation HEAD: `32505d049e3db80c3fd34581fab33c4033572e48`
- Workflow: `PR .NET Tests`
- Run: `30721897811`
- Conclusion: success
- Artifact: `8825122433`
- Artifact name: `ssc-pr-test-results-30721897811-1`
- Artifact digest: `sha256:e4bcf7f26b6d9460c0283de5f45d50e42e5ba9605895a491564b7168295c9457`
- Artifact `workflow_run.head_sha`: `32505d049e3db80c3fd34581fab33c4033572e48`

### Test result

- SSC.E2E.Tests: 88 passed / 0 failed / 0 skipped
- SSC.Unit.Tests: 100 passed / 0 failed / 0 skipped
- Total: 188 passed

### Coverage result

ReportGenerator `Summary.txt`の実測値です。

- Assemblies: 1
- Classes: 43
- Files: 19
- Line coverage: 87.7% (`1748 / 1992`)
- Branch coverage: 82.2% (`862 / 1048`)
- Method coverage: 92.6% (`291 / 314`)
- Fully covered methods: 73.5% (`231 / 314`)
- Uncovered methods: 23
- Uncovered lines: 244
- Report tag: `32505d049e3db80c3fd34581fab33c4033572e48`

coverage reportに現れたassemblyは`SSC`のみでした。`SSC.Generators`はAnalyzerとしてcompile時に実行されますが、今回のVSTest testhost coverageには含まれていません。generator本体をcoverage対象にする場合は、Roslyn generatorをtest process内で直接実行するtest構成が別途必要です。

## Artifact実体確認

次のfileを展開済みZIP内で確認しました。

- `coverage/raw/tests_SSC.E2E.Tests_SSC.E2E.Tests-1.cobertura.xml`
- `coverage/raw/tests_SSC.Unit.Tests_SSC.Unit.Tests-1.cobertura.xml`
- `coverage/report/Cobertura.xml`
- `coverage/report/index.html`
- class別HTML 43件
- `coverage/report/SummaryGithub.md`
- `coverage/report/Summary.txt`
- `coverage/logs/reportgenerator-install.stdout.log`
- `coverage/logs/reportgenerator-install.stderr.log`
- `coverage/logs/reportgenerator.stdout.log`
- `coverage/logs/reportgenerator.stderr.log`
- `coverage/coverage-inputs.txt`
- Unit/E2E TRX
- Unit/E2E restore・test stdout/stderr
- generator restore・build stdout/stderr
- runner、dotnet、git、project list診断ログ
- checkout source tarとGit metadata
- `manifest.md`

ReportGenerator stderrは空で、raw inputはUnitとE2Eの2件でした。

## 通っていない関数の確認方法

1. PR #55のmatching-HEAD runを開く
2. Artifact `ssc-pr-test-results-<run-id>-<attempt>`をダウンロード
3. ZIPを展開
4. `coverage/report/index.html`をブラウザで開く
5. Summary tableをline coverageの低い順に並べる
6. classを開き、Metrics tableでmethod coverageを確認
7. source表示で赤い未実行行、黄色の部分分岐、hit countを確認

判定の読み方:

- method `0%`: method内のcoverable lineが一度も実行されていない
- method `0%超100%未満`: methodは実行されたが、未実行行または未実行branchがある
- method `100%`: coverable lineは全実行。ただし全入力条件やassertの妥当性は保証しない

今回のmerged Coberturaで`0%`だった例:

- `SSC.ValueStateExtensions.IsMatched(ValueState)`
- `SSC.ValueStateExtensions.IsMismatched(ValueState)`
- `SSC.ValueStateExtensions.IsMissing(ValueState)`
- `SSC.ParallelCompareApi.FormatRuntimeTypes(...)`
- `SSC.ParallelCompareApi.ThrowDynamicContainerAccessError(...)`
- `SSC.DynamicParallelListView.get_Count()`
- `SSC.ParallelGeneratedDictionary<T1,T2,T3>.SelectModel(int)`
- `SSC.ParallelGeneratedList<T1,T2>.AtIndex(int)`

部分coverageの例:

- `SSC.ParallelCompareApi.EnumerateDictionary()` 30.8%
- `SSC.ParallelGeneratedDictionary<T1,T2,T3>.ResolveByKeyText(...)` 40.0%
- `SSC.ParallelNodeExtensions.GetDirectMemberName(...)` 40.0%
- `SSC.ParallelCompareApi.TryBuildDynamicContainerChildren(...)` 57.1%

これらは追加test候補を示しますが、coverage率だけを根拠にtestの必要性や優先度を決めません。public契約、異常系、境界値、実利用経路、assert内容を合わせて判断します。

## 意図的に変更していない範囲

- production C# code
- public API
- package version
- coverage threshold gate
- coverage badge
- external coverage SaaS
- GitHub Pages
- coverage historyまたはdiff coverage
- generatorをtest process内で直接実行するtest構成

## 残存リスク

- ReportGeneratorのGitHub Markdownではmethod coverage表示がスポンサー機能への案内になります。method別確認はartifact内HTMLまたはCoberturaを使用します。
- HTML reportはartifactとして提供するため、GitHub UI上で直接hostされません。ダウンロードしてローカルで開く必要があります。
- `SSC.Generators`の行coverageは今回取得できていません。
- coverage閾値を設けていないため、将来coverageが低下してもCIはcoverage欠落以外では失敗しません。

## 次のアクション

- PR #55を独立レビューする
- 必要な指摘がある場合は同一PR branchで修正する
- mergeはrepository ownerが実施する
