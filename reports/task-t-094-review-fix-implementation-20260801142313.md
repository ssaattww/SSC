# Sub-agent実行レポート

## タスク

- 目的: T-094として `PR49-FR1 [Medium][Required]` をTDDで修正する
- タスク種別: review follow-up implementation

## sub-agentを使う理由

- 理由: ユーザーが実装担当を `gpt-5.6-terra / high` に指定し、`codex-delegation-executor` がこの境界の明確な修正をsub-agent実装へ割り当てたため

## 対象範囲

- 対象: 空文字列CompareKeyが生成するlegacy標準pathに対する祖先pattern照合、TDD回帰test、必要な設計・互換性・XML documentation同期

## 対象外

- 対象外: path grammar全体の公開拡張、空selector patternの許可、無関係なparser/projection再設計、tracking編集、commit、push、review、merge

## 実行コマンド

- 実行コマンド:
  - Red: `dotnet test tests\SSC.E2E.Tests\SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GetDiffEntries_ReturnsEntryForEmptyCompareKey"`（exit 1、対象1件中1件失敗）
  - Green: `dotnet test tests\SSC.E2E.Tests\SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GetDiffEntries_ReturnsEntryForEmptyCompareKey"`（exit 0、対象1件中1件合格）
  - Green sibling regression: `dotnet test tests\SSC.Unit.Tests\SSC.Unit.Tests.csproj --configuration Release --filter "FullyQualifiedName~IsMatch_WithAncestorBeforeLegacyEmptyKeySelector_MatchesDescendantPath"`（exit 0、対象1件中1件合格）
  - Green E2E再実行: `dotnet test tests\SSC.E2E.Tests\SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GetDiffEntries_ReturnsEntryForEmptyCompareKey"`（exit 0、対象1件中1件合格）
  - `dotnet test SSC.sln --configuration Release`（exit 0、E2E 88件・Unit 86件合格）
  - `dotnet format SSC.sln --verify-no-changes`（exit 0）
  - `git diff --check`（exit 0、改行コード変換に関するGit warningのみ）
  - Markdown focused/full: repository に `tools/lint/`、`package.json`、`lint:md` がなく、shared script が要求する target/whitelist/cspell 設定も存在しないため未実行（unsupported）

## 対象ファイル

- 変更または確認したファイル:
  - `src/SSC/ParallelDiffPathPattern.cs`: legacy 空 selector を候補pathとして照合する内部parser経路を使用し、XML documentationを同期
  - `src/SSC/Internal/XPathLikePathParser.cs`: public既定grammarを変えず、空文字列CompareKeyの既存差分path専用の内部解析経路を追加
  - `tests/SSC.E2E.Tests/XPathLikeDiffEntriesE2ETests.cs`: 実際の `GetDiffEntries()` が生成する `Items[].Label` に `Items[*]` が一致するTDD回帰を追加
  - `tests/SSC.Unit.Tests/ParallelDiffPathPatternAncestorUnitTests.cs`: `Root.Items[].Label` の後続legacy selectorが上位祖先 `Root` の照合を阻害しないsibling回帰を追加
  - `doc/design/detail/10-DiffEntryPathFilter.md`: legacy候補path照合とpattern grammar境界を記録
  - `doc/design/detail/11-DiffEntryCustomPath.md`: 共有matcherにおけるlegacy候補pathの契約を記録
  - `Design/BreakingChanges.md`: Issue #48のruntime behavior拡張とpattern grammar不変を追記
  - `reports/task-t-094-review-fix-implementation-20260801142313.md`: 実装・検証証拠を記録

## 指摘事項

- 指摘要約または「指摘なし」:
  - PR49-FR1: `ParallelDiffPathPattern.IsMatch` が候補path全体を通常grammarで解析するため、空文字列CompareKeyから既存生成される `Items[].Label` を解析できず、`Items[*]` を含む有効な祖先patternが子孫差分に一致しなかった。

## 結果

- 結果:
  - Redで実際の `GetDiffEntries()` 出力に対する不一致を再現した後、matcher専用の内部解析で `[]` を空 key selector として扱う最小修正を実施した。
  - `ParallelDiffPathPattern.TryParse("Items[]") == false` と `Parse("Items[]")` が例外を送出する既存testは変更せず、公開pattern grammarを維持した。
  - 有効な上位祖先 `Root` が、後続にlegacy空 selectorを持つ `Root.Items[].Label` に一致することをUnit回帰で固定し、既存の空CompareKey E2Eも再実行してGreenを確認した。
  - selector種別、segment境界、escape、通常の不正path処理、および標準path/利用側定義pathの分離は変更していない。
  - commit、push、PRコメント、merge、独立review verdictは実施していない。最終HEADは `a3941d6cda44c44dd75f24394c4dfd7bdafb6838`。

## リスク

- 未解決のリスクまたは後続対応:
  - current HEADに一致するCI runは未作成のため、CI結果は未確認である。
  - Markdown lintはrepository wiring不在のためfocused/fullとも unsupported であり、passとして扱わない。
  - `Items[]` は公開patternとして不正のままで、matcher候補pathに限る既存互換の空 key selector 解釈である。
