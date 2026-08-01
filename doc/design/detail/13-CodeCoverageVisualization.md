# コードカバレッジ可視化

## 目的

Unit TestsとE2E TestsがSSCのproduction codeをどこまで実行したかを、PRのGitHub Actions SummaryとHTMLレポートで確認できるようにします。

コードカバレッジは、テスト実行中に通過した行、分岐、メソッドを示す補助指標です。カバレッジ率が高いことだけでは、assertの妥当性、境界値、異常系、仕様適合性は保証されません。

## CIで生成するもの

PR用workflowは各test projectを次の指定で実行します。

```bash
dotnet test <test-project> \
  --configuration Release \
  --collect:"XPlat Code Coverage"
```

生成されたCoberturaをReportGenerator 5.5.10で統合し、既存の診断artifact `ssc-pr-test-results-<run-id>-<attempt>` に保存します。

- `coverage/raw/*.cobertura.xml`: test projectごとのraw coverage
- `coverage/report/index.html`: class、method、source行を確認するHTML report
- `coverage/report/Cobertura.xml`: 統合済みcoverage
- `coverage/mobile/code-coverage.html`: スマートフォン向け単一HTML
- `coverage/report/*Github*.md`: Actions Summaryへ転記するMarkdown
- `coverage/report/*Summary*.txt`: text summary
- `coverage/logs/reportgenerator-install.stdout.log`
- `coverage/logs/reportgenerator-install.stderr.log`
- `coverage/logs/reportgenerator.stdout.log`
- `coverage/logs/reportgenerator.stderr.log`
- `coverage/coverage-inputs.txt`: 統合に使用したraw coverage一覧

既存のTRX、testの標準出力・標準エラー、診断ログ、checkout済みsourceも同じartifactに残します。

## PRで確認する方法

### スマートフォン

PR本文またはActions Summaryの「Mobile coverage report」をタップします。GitHub Pagesの固定URLから単一HTMLを開くため、artifact ZIPのダウンロードや展開は不要です。公開URLは `https://ssaattww.github.io/SSC/` です。

単一HTMLでは次を確認できます。

- line、branch、method coverage
- class別coverage
- method別coverage
- 0% methodと部分coverage method
- 未実行行番号
- methodからGitHub上のsourceへのリンク

### GitHub Actions

1. PRのChecksから `PR .NET Tests` を開きます。
2. 対象runのhead SHAがPRのcurrent HEAD SHAと一致することを確認します。
3. Job Summaryでline coverage、branch coverage、assembly別coverageを確認します。
4. より詳細なReportGenerator画面が必要な場合だけartifactを取得し、`coverage/report/index.html` を開きます。

## HTMLの保存・公開とCI停止条件

テストとcoverage生成が成功すると、専用jobが同じ単一HTMLを次の2箇所へ反映します。

- `gh-pages` branch直下の`index.html`: リポジトリ内の永続的なレポート保存場所
- GitHub Pages: スマートフォン向けの閲覧画面

PR番号をpathやbranch名へ含めず、常に最新の成功レポートを固定URLで公開します。`gh-pages` branchには`source-head.txt`と`source-pr.txt`も保存し、どのPR HEADから生成したかを追跡できるようにします。

PR branchへはコミットしません。テストjobのtokenは`contents: read`のまま維持し、公開jobだけに`contents: write`、`pages: write`、`id-token: write`を付与します。fork由来のPRでは公開しません。

PR workflowのtriggerは`pull_request`のみです。`gh-pages` branchへのpushはPR HEADを変更せず、workflowの再実行条件にも一致しません。そのためcoverage公開による再実行や無限CIは発生しません。公開jobには`gh-pages`を単位とするconcurrencyも設定し、複数runの公開競合を抑止します。

HTML公開直前にremote PR branchのHEADが元のPR HEADと一致することを検査します。作業中にPR HEADが更新されていた場合は、古いcoverageをbranchにもPagesにも公開せず、新しいHEAD側のrunへ処理を譲ります。

### 採用経緯

最初にPR branchの`reports/code-coverage.html`をActionsが自動更新する方式を試したが、bot commitでPR HEADが変わり、そのcommitに対するworkflowが`action_required`となった。PR HEAD一致CIの証跡が不安定になり、再実行制御を誤るとCI loopへつながるため採用しなかった。

次にartifact ZIPと外部HTML previewを検討したが、スマートフォンでZIPを展開する負担と、第三者preview serviceへの依存が残った。最終的に、PR HEADを変更しない専用branchを保存場所とし、公式の`actions/upload-pages-artifact`と`actions/deploy-pages`で同じHTMLをGitHub Pagesへ公開する方式を採用した。

## 通っていない関数を確認する方法

`coverage/report/index.html` のsummary tableでは、coverageが低いclassを並べ替えまたは絞り込みできます。classを開くと、Metricsにmethodごとのcoverageが表示されます。

- `0%` のmethod: そのmethod内のcoverable lineが一度も実行されていません。
- `0%`より大きく`100%`未満のmethod: method自体は呼ばれていますが、通っていない行または分岐があります。
- `100%` のmethod: coverable lineはすべて実行されています。ただし、全入力条件やassertの正しさを保証するものではありません。

同じdetails pageのsource表示では、各行の実行状況とhit countを確認できます。未実行行と未実行分岐を見て、追加すべき入力条件や異常系testを判断します。

### 確認例

1. Summaryでline coverageの低いclassを選択します。
2. Metricsで`0%`または部分coverageのmethodを探します。
3. source表示で未実行行を確認します。
4. `if`、`switch`、例外、早期returnなどの未実行経路に対応するtest caseを追加します。
5. coverageだけでなく、期待結果を検証するassertがあることを確認します。

## ローカルで生成する方法

リポジトリrootで次を実行します。

```bash
rm -rf artifacts/local-coverage
mkdir -p artifacts/local-coverage/test-results

dotnet test SSC.sln \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory artifacts/local-coverage/test-results

dotnet tool install dotnet-reportgenerator-globaltool \
  --tool-path artifacts/local-coverage/tools \
  --version 5.5.10

artifacts/local-coverage/tools/reportgenerator \
  "-reports:artifacts/local-coverage/test-results/**/coverage.cobertura.xml" \
  "-targetdir:artifacts/local-coverage/report" \
  "-reporttypes:Html;Cobertura;MarkdownSummaryGithub;TextSummary" \
  "-assemblyfilters:+SSC;+SSC.Generators" \
  "-sourcedirs:$PWD" \
  "-title:SSC code coverage"
```

生成後、`artifacts/local-coverage/report/index.html` を開きます。スマートフォン向け単一HTMLは次で生成できます。

```bash
python3 scripts/generate-mobile-coverage-report.py \
  --input artifacts/local-coverage/report/Cobertura.xml \
  --output reports/code-coverage.html \
  --repository ssaattww/SSC \
  --ref "$(git rev-parse HEAD)"
```

## 対象assembly

- `SSC`: 必須対象です。
- `SSC.Generators`: coverage dataに含まれる場合は同じreportへ表示します。

`SSC.Generators` はE2E TestsからAnalyzerとして参照されます。compile時のAnalyzer実行は通常のtesthostによる行coverageとして取得できない場合があります。その場合、reportにassemblyが現れないことを取得済みと誤認しません。generator本体のcoverageが必要な場合は、Roslyn generatorをtest process内で直接実行するtest構成を別途追加します。

## Failure時の扱い

- test成功時にCoberturaが1件も生成されなければworkflowを失敗させます。
- test失敗時でも、生成済みraw coverage、test logs、ReportGenerator logsを可能な限りartifactへ保存します。
- coverage report生成失敗で元のtest失敗を成功扱いにはしません。
