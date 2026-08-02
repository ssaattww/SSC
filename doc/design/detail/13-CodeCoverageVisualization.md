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
- `coverage/report/index.html`: ReportGeneratorの詳細HTML report
- `coverage/report/Cobertura.xml`: 統合済みcoverage
- `coverage/mobile/code-coverage.html`: スマートフォン向けの集計・ファイル一覧ページ
- `coverage/mobile/files/*.html`: source fileごとの行別coverageページ
- `coverage/report/*Github*.md`: Actions Summaryへ転記するMarkdown
- `coverage/report/*Summary*.txt`: text summary
- `coverage/logs/reportgenerator-install.stdout.log`
- `coverage/logs/reportgenerator-install.stderr.log`
- `coverage/logs/reportgenerator.stdout.log`
- `coverage/logs/reportgenerator.stderr.log`
- `coverage/coverage-inputs.txt`: 統合に使用したraw coverage一覧

既存のTRX、testの標準出力・標準エラー、診断ログ、checkout済みsourceも同じartifactに残します。

スマートフォン向けreportは、巨大な1ページへ全sourceを連結しません。`code-coverage.html`には全体集計、class・method一覧、source file一覧だけを置き、各source fileの行別表示は`files/*.html`へ分割します。ファイル名の衝突を避けるため、個別HTML名にはsource pathから生成した短いhashを付与します。

## PRで確認する方法

### スマートフォン

PR本文またはActions Summaryの「Mobile coverage report」をタップします。artifact ZIPのダウンロードや展開は不要です。公開URLは `https://ssaattww.github.io/SSC/` です。

トップページでは次を確認できます。

- line、branch、method coverage
- class別・method別coverage
- `行カバー済み`、`一部カバー`、`未実行`の状態
- source file一覧と、各ファイルの実行済み・未実行・対象外行数

source fileをタップすると、そのファイル専用ページへ移動します。専用ページには、そのファイルのsource行だけを表示します。

- `実行済み`: Coberturaのhit数が1以上
- `未実行`: coverable lineだがhit数が0
- `対象外`: Coberturaに実行対象として記録されていない行
- 行番号、hit count、source text
- source text検索と行状態filter
- 一覧へ戻るlinkとGitHub上のsourceへのlink

method名のlinkは、対応するファイル別ページとsource行anchorへ直接移動します。

### GitHub Actions

1. PRのChecksから `PR .NET Tests` を開きます。
2. 対象runのhead SHAがPRのcurrent HEAD SHAと一致することを確認します。
3. Job Summaryでline coverage、branch coverage、assembly別coverageを確認します。
4. より詳細なReportGenerator画面が必要な場合だけartifactを取得し、`coverage/report/index.html` を開きます。

## HTMLの保存・公開とCI停止条件

テストとcoverage生成が成功すると、専用jobがスマートフォン向けreport一式を次の2箇所へ反映します。

- `gh-pages` branch
  - `index.html`: 集計・ファイル一覧ページ
  - `files/*.html`: source fileごとの行別coverageページ
  - `source-head.txt`: report生成元のPR HEAD SHA
  - `source-pr.txt`: report生成元のPR番号
- GitHub Pages
  - `https://ssaattww.github.io/SSC/`: 集計・ファイル一覧
  - `https://ssaattww.github.io/SSC/files/<file-page>.html`: ファイル別行coverage

workflowは`coverage/mobile/`をディレクトリ単位でPages artifactと`gh-pages`へコピーします。トップページだけをコピーして個別ページが404になる状態を防ぐため、生成後と公開前の両方で`files/*.html`が1件以上存在することを検証します。

PR番号をpathやbranch名へ含めず、常に最新の成功レポートを固定URLで公開します。PR branchへはコミットしません。テストjobのtokenは`contents: read`のまま維持し、公開jobだけに`contents: write`、`pages: write`、`id-token: write`を付与します。fork由来のPRでは公開しません。

PR workflowのtriggerは`pull_request`のみです。`gh-pages` branchへのpushはPR HEADを変更せず、workflowの再実行条件にも一致しません。そのためcoverage公開による再実行や無限CIは発生しません。公開jobには`gh-pages`を単位とするconcurrencyも設定し、複数runの公開競合を抑止します。

HTML公開直前にremote PR branchのHEADが元のPR HEADと一致することを検査します。作業中にPR HEADが更新されていた場合は、古いcoverageをbranchにもPagesにも公開せず、新しいHEAD側のrunへ処理を譲ります。

### 採用経緯

最初にPR branchの`reports/code-coverage.html`をActionsが自動更新する方式を試したが、bot commitでPR HEADが変わり、そのcommitに対するworkflowが`action_required`となった。PR HEAD一致CIの証跡が不安定になり、再実行制御を誤るとCI loopへつながるため採用しなかった。

次にartifact ZIPと外部HTML previewを検討したが、スマートフォンでZIPを展開する負担と、第三者preview serviceへの依存が残った。最終的に、PR HEADを変更しない`gh-pages` branchを保存場所とし、公式の`actions/upload-pages-artifact`と`actions/deploy-pages`でGitHub Pagesへ公開する方式を採用した。

当初のスマートフォン向けreportは全source行を単一HTMLへ連結していた。行単位の実行状態は確認できたが、ページが長く、ファイル境界が分かりにくく、目的のファイルへ移動した後も他ファイルの大量の行を保持していた。利用者のフィードバックを受け、トップページを一覧に限定し、source fileごとに独立したHTMLへ分割した。通常のlink・戻る操作・直接URLを使用でき、JavaScriptが無効でもファイル別ページ自体を表示できる物理HTML分割を採用した。

## 通っていない関数・行を確認する方法

トップページで`未実行`または`一部カバー`のmethodを探します。method名をタップすると、該当source fileの個別ページと対象行へ移動します。

- `0%` のmethod: そのmethod内のcoverable lineが一度も実行されていません。
- `0%`より大きく`100%`未満のmethod: method自体は呼ばれていますが、通っていない行または分岐があります。
- `100%` のmethod: coverable lineはすべて実行されています。ただし、全入力条件やassertの正しさを保証するものではありません。

ファイル別ページでは`行状態`を`未実行`へ変更すると、そのファイルの未実行行だけを確認できます。`if`、`switch`、例外、早期returnなどの未実行経路に対応するtest caseを追加し、期待結果を検証するassertがあることも確認します。

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

python3 scripts/generate-mobile-coverage-report.py \
  --input artifacts/local-coverage/report/Cobertura.xml \
  --output artifacts/local-coverage/mobile/code-coverage.html \
  --repository ssaattww/SSC \
  --ref "$(git rev-parse HEAD)" \
  --source-root "$PWD"
```

生成後、次を開きます。

- `artifacts/local-coverage/report/index.html`: ReportGenerator詳細画面
- `artifacts/local-coverage/mobile/code-coverage.html`: スマートフォン向け一覧
- `artifacts/local-coverage/mobile/files/*.html`: ファイル別行coverage

## 対象assembly

- `SSC`: 必須対象です。
- `SSC.Generators`: coverage dataに含まれる場合は同じreportへ表示します。

`SSC.Generators` はE2E TestsからAnalyzerとして参照されます。compile時のAnalyzer実行は通常のtesthostによる行coverageとして取得できない場合があります。その場合、reportにassemblyが現れないことを取得済みと誤認しません。generator本体のcoverageが必要な場合は、Roslyn generatorをtest process内で直接実行するtest構成を別途追加します。

## Failure時の扱い

- test成功時にCoberturaが1件も生成されなければworkflowを失敗させます。
- スマートフォン向け一覧または`files/*.html`が生成されなければworkflowを失敗させます。
- 公開artifactに一覧または個別ページがなければ公開jobを失敗させます。
- test失敗時でも、生成済みraw coverage、test logs、ReportGenerator logsを可能な限りartifactへ保存します。
- coverage report生成失敗で元のtest失敗を成功扱いにはしません。
