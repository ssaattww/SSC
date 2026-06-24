# T-084 Implementation Report

## 対象

- T-084: Source Generator の object member 展開不足修正
- 参照 gist: https://gist.github.com/ssaattww/fbd4fa683d03cd03dd73df0fa37bf1a0
- PR: #37

## 再現

更新後 gist の `global_Document.ParallelGenerated.g.cs` では、`Document.Root` が `ParallelGeneratedValue<Document, Node>` として生成され、`Root.Name` / `Root.Attribute` / `Root.Range` へ型付き generated view で進めない。

追加した regression test:

- `GeneratedProjectionE2ETests.Compare_GeneratedProjection_ObjectMember_GeneratesNestedViewMembers`

初回実行結果:

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ObjectMember_GeneratesNestedViewMembers"`
- 失敗
- 主なエラー: `ParallelGeneratedValue<GeneratedDocument, GeneratedXmlNode>` に `Name` / `Attribute` / `Range` が存在しない

## 実装

- `src/SSC.Generators/ParallelViewGenerator.cs`
  - scalar / container 以外の class / struct / interface member を `Object` member として分類
  - object member の型も type graph に追加し、nested generated view を生成
  - object member accessor は `ParallelGeneratedRuntime.RequireMemberNode<TParent, TMember>()` で既存比較ツリーの member node を取得して view 化
  - generated view 自体に `GetState(modelIndex)` と `Select(...)` を追加
- `src/SSC/GeneratedProjectionRuntime.cs`
  - generated object member 用 helper `RequireMemberNode<TParent, TMember>()` を追加
  - 性能優先で `GetDirectChildren()` 経由ではなく内部 member-node dictionary を直接参照
- `tests/SSC.E2E.Tests/GeneratedProjectionE2ETests.cs`
  - gist の `Document.Root` に相当する最小モデルを追加
  - `root.Root.Name` / `root.Root.Attribute` / `root.Root.Range` を型付き generated view で辿ることを検証
- `doc/design/detail/02-PublicApi.md`
  - Source Generator が class / struct object member を direct nested view として生成する契約を追記

## 検証

- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests.Compare_GeneratedProjection_ObjectMember_GeneratesNestedViewMembers"`
  - 成功: 1 件
- `dotnet test tests/SSC.E2E.Tests/SSC.E2E.Tests.csproj --configuration Release --filter "FullyQualifiedName~GeneratedProjectionE2ETests"`
  - 成功: 9 件
- `dotnet test SSC.sln --configuration Release`
  - 成功: Unit 29 件 / E2E 64 件
- `dotnet format SSC.sln --verify-no-changes`
  - 成功
- `git diff --check`
  - 成功
- `npm run lint:md`
  - 失敗: `Missing script: "lint:md"`
  - repo 側に Markdown lint script / 設定がないため unsupported と判定
- gpt-5.5 high sub-agent review
  - 通常パスを壊す実装バグなし
  - generated API source shape 変更は breaking change として `Design/BreakingChanges.md` へ記録

## 残リスク

- Markdown lint はリポジトリ側に実行 script / 設定がないため unsupported。
