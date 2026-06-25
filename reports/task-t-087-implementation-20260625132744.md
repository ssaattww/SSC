# Sub-agent実行レポート

## タスク
T-087: generated projection list の key text indexer 追加

## sub-agentを使う理由
ユーザー報告の CustomXML Attribute key access 不具合について、TDD による実装を sub-agent に委譲するため。

## 対象範囲
- `ParallelGeneratedList<TElement, TView>` に `this[string keyText]` を追加する
- key text lookup を generated list instance 内で cache する
- missing key を `CompareExecutionException` / `CompareIssueCode.KeyNotFound` で失敗させる
- CustomXML の `root.Root.Attribute["id"]` key access を E2E で検証する

## 対象外
- dynamic projection の key access 変更
- container normalization semantics の変更
- Source Generator の model shape 変更
- PR 作成、最終 review、tracking 完了処理

## 実行コマンド
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests|FullyQualifiedName~GeneratedProjectionE2ETests"`（実装前 failing proof: `root.Root.Attribute["id"].Value[0]` / `root.Groups["1"]` などが `CS1503: Argument 1: cannot convert from 'string' to 'int'`、`CompareIssueCode.KeyNotFound` が `CS0117`）
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests|FullyQualifiedName~GeneratedProjectionE2ETests"`（実装後: Passed 11 / Failed 0 / Skipped 0）
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests"`（追加要件実装前 failing proof: diff path selector `A\]B` が `KeyNotFound`）
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests|FullyQualifiedName~GeneratedProjectionE2ETests"`（追加要件実装後: Passed 12 / Failed 0 / Skipped 0）
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~XmlCustomGeneratedCompareE2ETests|FullyQualifiedName~GeneratedProjectionE2ETests"`（レビュー対応後: Passed 12 / Failed 0 / Skipped 0）
- `git diff --check`（成功）

## 対象ファイル
- `src/SSC/Contracts.cs`
- `src/SSC/GeneratedProjectionRuntime.cs`
- `tests/SSC.E2E.Tests/XmlCustomGeneratedCompareE2ETests.cs`
- `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`

## 指摘事項
- 実装前は generated list に `string` indexer がなく、CustomXML の `Attribute["id"]` と通常 generated projection container の key text access が compile error になった。
- `CompareIssueCode.KeyNotFound` も未定義だったため、missing key の契約を表現できなかった。
- 追加要件の実装前は raw `NodeMeta.KeyText` のみ検索していたため、`GetDiffEntries()` の `Attribute[A\]B]` から取り出した bracket text `A\]B` では generated access できなかった。
- レビューで `CompareIssueCode.KeyNotFound` の enum 追加位置、escaped discriminator と raw key の曖昧性、テスト helper の selector 抽出ロジックが指摘された。

## 結果
- `CompareIssueCode.KeyNotFound` を追加した。
- `ParallelGeneratedList<TElement, TView>` に `this[string keyText]` を追加し、`_nodes[index].KeyText` を `StringComparer.Ordinal` の lazy `Dictionary<string, int>` cache で検索するようにした。
- key 未検出時は `CompareExecutionException` / `CompareIssueCode.KeyNotFound` を投げるようにした。
- CustomXML の `root.Root.Attribute["id"].Value[0]` と、通常 generated projection の `Groups["1"]` / `Items["100"]` / `Scores["A"]` を E2E で検証した。
- missing key は `CompareIssueCode.KeyNotFound` の `CompareExecutionException` になることを E2E で検証した。
- 既存の index access と `SelectModel` は対象 E2E 内で継続して検証され、成功した。
- raw key lookup で未検出の場合に XPath-like escaped discriminator として `\]` / `\\` / `\#` を unescape し、再検索する fallback を追加した。
- `GetDiffEntries()` の path から bracket 内 selector を取り出し、`root.Root.Attribute[selector]` で `A]B` と `#0` の dictionary child にアクセスできることを E2E で検証した。
- `CompareIssueCode.KeyNotFound` を enum 末尾へ移動し、既存 underlying value を維持した。
- 有効な XPath-like escape を含む入力では unescape lookup を raw lookup より優先し、見つからない場合だけ raw lookup に fallback するようにした。
- `A]B` と raw key `A\]B` が共存するケースを E2E に追加し、diff path selector `A\]B` は `A]B` 側へ、raw key `A\]B` の diff path selector は raw key 側へ解決されることを検証した。
- テスト helper の selector 抽出を bracket 内 escape state で閉じ bracket を判定する実装へ変更した。

## リスク
- 検証は指定された E2E フィルタと `git diff --check` に限定した。全 test suite は未実行。
- key index cache は generated list instance 単位の lazy 構築で、同一 key が万一重複した場合は最初の node を保持する。通常の normalized container では key union により重複しない前提。
- 不正 escape は raw key としても見つからない場合に `KeyNotFound` として失敗する。object/composite key の完全復元は対象外。
