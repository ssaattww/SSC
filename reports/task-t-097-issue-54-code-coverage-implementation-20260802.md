# T-097 Issue #54 コードカバレッジ視覚化 実装レポート

## 1. 対象

- Repository: `ssaattww/SSC`
- Issue: #54
- Pull Request: #55
- Branch: `feature/issue-54-code-coverage`
- Base: `main`
- 検証済みimplementation HEAD: `32505d049e3db80c3fd34581fab33c4033572e48`
- Matching workflow run: `30721897811`
- Artifact: `8825122433` (`ssc-pr-test-results-30721897811-1`)

## 2. 目的と範囲

Unit TestsとE2E TestsがSSCのproduction codeをどこまで実行したかを、GitHub Actions SummaryとHTML reportで確認できるようにした。

対象はcoverage収集、複数test projectの統合、Summary表示、source行単位のHTML可視化、既存診断artifactへの保存である。coverage閾値、badge、履歴、外部SaaS、GitHub Pagesは対象外とした。

## 3. 開発方針

SSCの通常方針はTDDだが、利用者から「tddせず実装」と明示指示があったため、Red-firstは実施せずtest-afterでworkflow契約testを追加した。Red/Green evidenceは捏造していない。

## 4. 実装

### 4.1 Coverage収集

`.github/workflows/pr-xunit-tests.yml` の各 `dotnet test` に `--collect:"XPlat Code Coverage"` を追加した。各test projectの結果を個別directoryへ保存し、Coberturaを `coverage/raw` へコピーする。VSTestが同一reportを複製した場合はSHA-256で重複排除する。

### 4.2 ReportGenerator

`dotnet-reportgenerator-globaltool` 5.5.10をversion固定で導入し、次を生成する。

- HTML report
- merged Cobertura
- `MarkdownSummaryGithub`
- `TextSummary`
- install／generateのstdout、stderr
- 入力coverage file一覧

### 4.3 Actions Summaryとartifact

Actions SummaryへPR head SHA、workflow commit、line／branch／assembly別coverage、HTML reportのartifact pathを出力する。既存のTRX、restore/test stdout・stderr、診断ログ、source archiveと同じartifactへcoverage一式を保存する。

### 4.4 Coverage欠落

test成功時にraw Coberturaが存在しなければCIを失敗させる。test失敗時は生成済みcoverageとログを可能な限りartifactへ残し、元のtest failureを隠さない。

## 5. 変更ファイル

- `.github/workflows/pr-xunit-tests.yml`
- `tests/SSC.Unit.Tests/Issue54CodeCoverageWorkflowContractUnitTests.cs`
- `tests/SSC.Unit.Tests/GitHubActionsTestArtifactContractUnitTests.cs`
- `doc/design/detail/13-CodeCoverageVisualization.md`
- `doc/design/README.md`
- `README.md`
- 本レポート

production APIとproduction source codeは変更していない。

## 6. 失敗と修正

### 6.1 Run 30721697713

- Head: `5c0e5e710153a2e7f90e2b67803cb29454183de4`
- Conclusion: failure
- 原因: 既存artifact契約testが `mkdir -p "$results_dir/logs"` の完全一致を要求していた
- Artifact: `8825063372`
- 対応: 既存ログdirectory契約を維持した

### 6.2 Run 30721792856

- Head: `f3576c17c5e6afb91c0fcab697c3faf5c3ca675b`
- Conclusion: failure
- 原因: 既存artifact契約testがtest result保存先を旧 `results_dir` に固定していた
- Artifact: `8825091007`
- 対応: project別結果directoryが既存artifact配下に存在することを契約testで検証するよう更新した

両runともcoverage report生成とartifact upload自体は成功し、原因調査に必要なTRX、stdout、stderr、coverage、ReportGenerator logが保存された。

## 7. 検証

Implementation HEAD `32505d049e3db80c3fd34581fab33c4033572e48` と一致するworkflow run `30721897811`を確認した。別SHAのrunは代用していない。

- Workflow: `PR .NET Tests`
- Conclusion: success
- Unit Tests: 100件成功
- E2E Tests: 88件成功
- Artifact digest: `sha256:e4bcf7f26b6d9460c0283de5f45d50e42e5ba9605895a491564b7168295c9457`
- raw Cobertura: 2件
- Assemblies: 1 (`SSC`)
- Classes: 43
- Files: 19
- Line coverage: 87.7% (`1748 / 1992`)
- Branch coverage: 82.2% (`862 / 1048`)
- Method coverage: 92.6% (`291 / 314`)
- Fully covered methods: 73.5% (`231 / 314`)

Artifact内で次を実体確認した。

- `coverage/report/index.html`
- class別HTML 43件
- `coverage/report/Cobertura.xml`
- `coverage/report/SummaryGithub.md`
- `coverage/report/Summary.txt`
- raw Cobertura 2件
- ReportGenerator install／generate stdout・stderr
- Unit／E2E TRX
- restore／test stdout・stderr
- source archiveとGit metadata
- manifestへのcoverage payload列挙

Runner環境にはlocal `dotnet` がないため、別のlocal `dotnet format` は実行できなかった。PR workflow内のbuildと全testを検証根拠とした。

## 8. 通っていない関数の確認方法

1. PR #55のmatching runのartifactをダウンロードする。
2. ZIPを展開し、`coverage/report/index.html` を開く。
3. Summaryでcoverageの低いclassを選択する。
4. class pageのmethod tableでline coverageを確認する。
5. `0%`ならmethod内のcoverable lineが未実行、`0%`より大きく`100%`未満なら未実行行または未実行分岐が残る。
6. 同じpageのsource表示で未実行行とhit countを確認する。

今回のartifactでは未実行methodが23件ある。例として `ValueStateExtensions.IsMissing`、`IsMatched`、`IsMismatched`、`ParallelCompareApi.FormatRuntimeTypes`、`DynamicParallelListView.get_Count` が0%として表示された。

## 9. 残留事項

- `SSC.Generators` はAnalyzerとしてcompile時に実行されるため、通常のtesthost coverageにはassemblyとして現れなかった。generator本体coverageが必要ならRoslyn generatorをtest process内で直接実行するtest構成が別途必要である。
- coverage率はtest品質、assert妥当性、仕様適合性を単独では保証しない。
- 本レポートとREADME追加後のPR current HEADには新しいmatching CIが必要であり、その結果はPRコメントへ記録する。
- `tasks/tasks-status.md` と `tasks/phases-status.md` は全文置換が必要なconnector制約のため本実装では変更していない。Issue #54とPR #55を作業identityとする。
- 独立レビューは実装者とは別のreview workerで行う。

## 10. マージ境界

mergeは実施していない。利用者がレビュー完了後に判断する。
