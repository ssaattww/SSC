# Sub-agent実行レポート

## タスク

- 目的: T-083 Sprache `.csx` XML-like compare sample の追加
- タスク種別: TDD 実装

## sub-agentを使う理由

- 理由: ユーザー指示の sample 実装であり、コード実装は sub-agent に委譲する。親 agent は scope 管理、report 確認、Git 操作を担当する。

## 対象範囲

- 対象: Sprache `2.3.1` を使う `.csx` sample、数字始まり element / attribute name を許可する parser、SSC 比較 sample、実行または代替検証

## 対象外

- 対象外: SSC runtime 本体の public API 変更、標準 XML 仕様準拠 parser 化、Markdown 検査、PR 操作、commit/push

## 実行コマンド

- 実行コマンド:
  - `test -f samples/xml-compare-sprache/compare-xml.csx`: 失敗（実装前）。sample file が未作成であることを確認。
  - `dotnet build src/SSC/SSC.csproj --configuration Release`: 成功。sample から参照する `SSC.dll` を生成。
  - `dotnet tool install --tool-path /tmp/dotnet-tools dotnet-script`: 成功。repo 外一時 tool path に `dotnet-script` 2.0.1 をインストール。
  - `/tmp/dotnet-tools/dotnet-script samples/xml-compare-sprache/compare-xml.csx`: 失敗。`/home/ibis/.cache/dotnet-script` が read-only のため cache 書き込み不可。
  - `XDG_CACHE_HOME=/tmp/dotnet-script-cache DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/tmp/nuget-packages /tmp/dotnet-tools/dotnet-script samples/xml-compare-sprache/compare-xml.csx`: 失敗。script の `equals` identifier と list 型変換を修正対象として検出。
  - `XDG_CACHE_HOME=/tmp/dotnet-script-cache DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/tmp/nuget-packages /tmp/dotnet-tools/dotnet-script samples/xml-compare-sprache/compare-xml.csx`: 成功。数字始まり element / attribute name を parse し、SSC diff entries を出力。
  - `dotnet script --version`: 成功。ユーザーが global install した `dotnet-script` 2.0.1 を確認。
  - `dotnet script samples/xml-compare-sprache/compare-xml.csx`: 失敗。global tool は利用可能だが、default cache path `/home/ibis/.cache/dotnet-script` が read-only のため cache 書き込み不可。
  - `XDG_CACHE_HOME=/tmp/dotnet-script-cache DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/tmp/nuget-packages dotnet script samples/xml-compare-sprache/compare-xml.csx`: 成功。global `dotnet script` でも数字始まり element / attribute name を parse し、SSC diff entries を出力。
  - `rg -n "XDocument|System.Xml|#r \"nuget: Sprache, 2\\.3\\.1\"|Parse\\.Char\\(IsNameChar|<1root 2id" samples/xml-compare-sprache/compare-xml.csx`: 成功。Sprache 2.3.1 参照、数字始まり input、name parser を確認し、`XDocument` / `System.Xml` 使用なしを確認。
  - `git diff --check`: 成功。
  - `XDG_CACHE_HOME=/tmp/dotnet-script-cache DOTNET_CLI_HOME=/tmp/dotnet-cli-home NUGET_PACKAGES=/tmp/nuget-packages dotnet script samples/xml-compare-sprache/compare-xml.csx`: 成功。follow-up 後、runner が NuGet `devo6.SSC, 0.3.1-pre` と `#load "xml-like-parser.csx"` を使う構成で実行できることを確認。
  - `rg -n "XDocument|System\\.Xml|XmlReader|#r \"nuget: devo6\\.SSC, 0\\.3\\.1-pre\"|#load \"xml-like-parser\\.csx\"|Parse\\.Char\\(IsNameChar|<1root 2id" samples/xml-compare-sprache`: 成功。NuGet SSC 参照、parser 分離、数字始まり input/name grammar を確認し、標準 XML parser 使用なしを確認。
  - `git diff --check`: 成功。follow-up 後の差分に whitespace error なし。
  - Markdown 検査: ユーザー指示により未実行。

## 対象ファイル

- 変更または確認したファイル:
  - `samples/xml-compare-sprache/compare-xml.csx`
  - `samples/xml-compare-sprache/xml-like-parser.csx`
  - `src/SSC/ParallelPathAccessExtensions.cs`
  - `src/SSC/ParallelDiffContracts.cs`
  - `README.md`
  - `reports/task-t-083-implementation-20260623132920.md`
  - `tasks/tasks-status.md`
  - `AGENTS.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。

## 結果

- 結果:
  - `samples/xml-compare-sprache/compare-xml.csx` を追加した。
  - `.csx` は `#r "nuget: Sprache, 2.3.1"` を使い、標準 XML parser ではなく Sprache parser combinator で XML-like input を parse する。
  - name grammar は最初の文字を含めて `A-Z` / `a-z` / digit / `_` / `:` / `-` を許可し、sample input の `<1root 2id="left">...</1root>` を受け付ける。
  - start tag / end tag name の一致を parser 内で検証する。
  - attributes は名前順に deterministic な model へ変換し、list item には `[CompareKey]` を設定した。
  - parser 結果を `XmlDocumentModel` / `XmlElementModel` / `XmlAttributeModel` に変換し、`ParallelCompareApi.Compare()` と `GetDiffEntries()` で差分 path / values / state を表示する。
  - 実行結果として、`Root.Attributes[2id].Value`、`Root.Children[1root/2item#0].Text`、`Root.Children[1root/2item#0].Attributes[3code].Value` など、数字始まり element / attribute name を含む diff entries が出力された。
  - follow-up で parser 実装を `samples/xml-compare-sprache/xml-like-parser.csx` に分離し、`compare-xml.csx` は実行 runner として NuGet `devo6.SSC, 0.3.1-pre` を参照する形に変更した。
  - follow-up 後も数字始まり element / attribute name の parse check と SSC diff entry 出力 check は維持されている。
  - SSC runtime 本体の public API 変更、`.sln` への sample project 追加、Markdown 検査、PR 操作、commit/push には踏み込んでいない。

## リスク

- 未解決のリスクまたは後続対応:
  - 未解決リスクなし。`dotnet-script` は repo 外の `/tmp/dotnet-tools` に一時インストールして検証した。
  - follow-up 後も未解決リスクなし。SSC 参照はローカル DLL ではなく NuGet package を使用している。
