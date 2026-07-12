# Sub-agent実行レポート

## タスク

- 目的: PR #45 の差分パスLINQフィルターを初回コードレビューする
- タスク種別: コードレビュー

## sub-agentを使う理由

- 理由: `review-enforcer` により独立したsub-agentレビューが必須であり、ユーザーが `gpt-5.6-sol / high` を指定したため

## 対象範囲

- 対象: `origin/main...HEAD` のproduction code、unit test、設計書、および周辺実装との整合
- レビュー基準: public API契約、path pattern grammar、matching semantics、例外契約、性能上の明白な問題、設計書との一致、日本語XML documentation、テストの十分性

## 対象外

- 対象外: レビュー中の実装修正、PR #45 と無関係な既存コードの改善、report構造の変更

## 実行コマンド

- 実行コマンド: `git status --short --branch`、`git log --oneline --decorate -8`、`git diff --stat origin/main...HEAD`、`git diff --name-status origin/main...HEAD`、`git diff --unified=80 origin/main...HEAD -- <3変更ファイル>`、`nl -ba`、`sed -n`、`rg -n`で差分と周辺実装・XML documentation・設計配置を確認した。`dotnet script eval` で literal `*` key の照合を再現し、`literal-star=True; other-key=True; escaped-star-parse=False` を確認した。`dotnet test tests/SSC.Unit.Tests/SSC.Unit.Tests.csproj --configuration Release --filter FullyQualifiedName~ParallelDiffPathPatternUnitTests` は16件成功、`dotnet test SSC.sln --configuration Release` はUnit 47件・E2E 81件の全128件成功、`dotnet format SSC.sln --verify-no-changes` と `git diff --check origin/main...HEAD` は成功した。`npm run lint:md` は `Missing script: "lint:md"` で失敗し、`tools/lint/`、`cspell.config.jsonc`、textlint/prh/whitelist設定も存在しないため、focused/full Markdown lintはともに `unsupported` と判定した。

## 対象ファイル

- 変更または確認したファイル: `src/SSC/ParallelDiffPathPattern.cs`、`tests/SSC.Unit.Tests/ParallelDiffPathPatternUnitTests.cs`、`doc/design/detail/09-DiffEntryPathFilter.md`、`src/SSC/Internal/XPathLikePathParser.cs`、`src/SSC/ParallelPathAccessExtensions.cs`、`src/SSC/ParallelDiffContracts.cs`、`tests/SSC.Unit.Tests/XPathLikePathParserUnitTests.cs`、`tests/SSC.Unit.Tests/XPathLikeDiffEntriesUnitTests.cs`、`doc/design/README.md`、`doc/design/detail/09-ValueStateBehavior.md`、`src/SSC/SSC.csproj`、`tests/SSC.Unit.Tests/SSC.Unit.Tests.csproj`、`package.json`、`tasks/tasks-status.md`、`tasks/phases-status.md`。変更したのは本レポートのみ。

## 指摘事項

- 指摘要約または「指摘なし」:
  - [P1][ユーザー確認必須のcapability gap] `src/SSC/ParallelDiffPathPattern.cs:122-134` は `[*]` を必ずwildcardとして解釈する一方、既存のpath生成は `src/SSC/ParallelPathAccessExtensions.cs:282-291` で `*` をescapeしない。そのため有効なliteral `*` keyのpath `Items[*].Value` をexact指定できず、同じpatternが `Items[other].Value` にも一致する。`Items[\*].Value` も既存escape grammarでは解析できない。LINQで差分を除外する主用途で無関係な差分を隠し得るため、literal wildcardのescape構文を追加するか、literal `*` keyを非サポートとして公開契約に明記するかをユーザに確認する必要がある。
  - [P2][blocking] `tests/SSC.Unit.Tests/ParallelDiffPathPatternUnitTests.cs:5-128` の新規test classと全10個の `[Fact]` / `[Theory]` にXML summaryがなく、method内の `// Intent:` はテスト契約のXML documentationを代替できない。また、grammar/matchingの境界を担う新規private関数 `src/SSC/ParallelDiffPathPattern.cs:94-153,156-228,242-255,272-298` にもタスク基準が要求する契約説明がない。さらにpublic `PathMatches` は `src/SSC/ParallelDiffPathPattern.cs:313-320` でentry/patternのnull時に `ArgumentNullException` を投げるが、XML documentationに両方の `<exception>` 契約がない。`source-documentation-policy` とT-092 exit criteriaに反するためreview gateをblockする。
  - [P2][blocking] `doc/design/detail/09-DiffEntryPathFilter.md:72-74` は `TryParse(string pattern, ...)` と非nullの公開signatureを記載するが、実装は `src/SSC/ParallelDiffPathPattern.cs:42` で `string?` を受け取り、同設計書の91行もnullを正式な `false` 経路と定義している。nullable契約を設計と実装で一致させる必要がある。加えて `doc/design/README.md:7-18` のFinal Design Indexに新規設計書が追加されず、既存 `09-ValueStateBehavior.md` と番号が衝突しているため、設計ソースの配置と索引を整合させる必要がある。
  - [P2][blocking] `tests/SSC.Unit.Tests/ParallelDiffPathPatternUnitTests.cs:67-98,127-136` は不正非null pattern、不正candidate path、extensionのnullだけを検証し、設計書の例外契約である `Parse(null)`、`TryParse(null)`、`IsMatch(null)` を検証していない。既存escape grammar維持についてもdotと `\]` のみで、`\\` と `\#` のexact key照合が未検証である。また `doc/design/detail/09-DiffEntryPathFilter.md:177-186` が明記する `Issues` / `Root.HasDifferences()` 不変と `CompareIgnore` 回帰は新規テストから直接確認できない。現行実装は差分一覧に状態を持たず全回帰テストも成功しているが、明文化したpublic boundaryと設計上のテスト方針を回帰テストに固定するまでreview gateをblockする。
  - [P3][non-blocking held] 候補pathは各 `IsMatch` で再解析されるが、patternは一度だけ構造化され、正規表現や明白な非線形処理もない。現スコープで明白な性能・安全性の追加指摘はなく、benchmark/cache追加は実害が出るまでholdとする。

## 結果

- 結果: 初回レビューは不合格。Release全128テスト、focused 16テスト、format、diff checkは成功したが、literal `*` keyとwildcardの契約衝突はユーザ確認が必要であり、XML documentation、設計書契約／配置、public boundaryのテストにblocking findingがある。修正後に同一レビュアーでの再レビューが必要。

## リスク

- 未解決のリスクまたは後続対応: literal `*` keyの公開grammar方針をユーザに確認し、決定に従って設計・実装・テストを一致させる必要がある。その他のblocking findingを修正してfocused/full validationと再レビューを実施する必要がある。Markdown lintはrepository wiring不在によりfocused/fullとも `unsupported` で、用語検査が自動実行できない残存リスクがあるが、設計本文の目視照合、backtick/quote回避の不在確認、実装・テストとの契約照合を行ったため、本初回レビューでは追加のnon-blocking held riskとする。
