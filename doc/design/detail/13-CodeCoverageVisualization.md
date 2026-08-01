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
- `coverage/report/*Github*.md`: Actions Summaryへ転記するMarkdown
- `coverage/report/*Summary*.txt`: text summary
- `coverage/logs/reportgenerator-install.stdout.log`
- `coverage/logs/reportgenerator-install.stderr.log`
- `coverage/logs/reportgenerator.stdout.log`
- `coverage/logs/reportgenerator.stderr.log`
- `coverage/coverage-inputs.txt`: 統合に使用したraw coverage一覧

既存のTRX、testの標準出力・標準エラー、診断ログ、checkout済みsourceも同じartifactに残します。

## PRで確認する方法

1. PRのChecksから `PR .NET Tests` を開きます。
2. 対象runのhead SHAがPRのcurrent HEAD SHAと一致することを確認します。
3. Job Summaryでline coverage、branch coverage、assembly別coverageを確認します。
4. Artifactsから `ssc-pr-test-results-<run-id>-<attempt>` をダウンロードします。
5. ZIPを展開し、`coverage/report/index.html` をブラウザで開きます。

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

生成後、`artifacts/local-coverage/report/index.html` を開きます。

## 対象assembly

- `SSC`: 必須対象です。
- `SSC.Generators`: coverage dataに含まれる場合は同じreportへ表示します。

`SSC.Generators` はE2E TestsからAnalyzerとして参照されます。compile時のAnalyzer実行は通常のtesthostによる行coverageとして取得できない場合があります。その場合、reportにassemblyが現れないことを取得済みと誤認しません。generator本体のcoverageが必要な場合は、Roslyn generatorをtest process内で直接実行するtest構成を別途追加します。

## Failure時の扱い

- test成功時にCoberturaが1件も生成されなければworkflowを失敗させます。
- test失敗時でも、生成済みraw coverage、test logs、ReportGenerator logsを可能な限りartifactへ保存します。
- coverage report生成失敗で元のtest失敗を成功扱いにはしません。
