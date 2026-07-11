# Diff Entry Path Filter

## 1. 目的

`CompareResult<T>.GetDiffEntries()` が返す差分一覧に対し、XPath-like path の構造を使って利用側で任意の差分を絞り込めるようにする。

想定例:

```text
Boards[任意].Files[任意].Document.Root.Children[任意].Children[任意].Attribute[LastEditingTime].Value
```

基板名、ファイル識別子、子要素のキーまたは ordinal が入力ごとに変化しても、構造上同じ位置にある差分を一致させる。

## 2. 非目的

- `[CompareIgnore]` の動作変更
- 比較対象メンバーの変更
- `ParallelCompareApi.Compare()` の比較結果変更
- `CompareConfiguration` への ignore path 設定追加
- `GetDiffEntries()` が返す差分一覧の変更
- `CompareResult<T>.Root`、`Issues`、`ValueState`、`HasDifferences()` の変更
- 正規表現による path 判定
- XPath の axis、predicate、再帰検索の実装

## 3. `CompareIgnore` との境界

`CompareIgnoreAttribute` はメタデータ解決時に対象メンバーを比較対象から除外する。除外されたメンバーは node 構築、container 正規化、差分生成の対象にならない。

本機能は比較完了後に生成済みの `ParallelDiffEntry` を選別するだけであり、`CompareIgnoreAttribute`、`TypeMetadataResolver`、比較パイプラインには接続しない。

```text
models
  -> CompareIgnore を含む既存メタデータ解決
  -> 既存比較ツリー構築
  -> 既存 GetDiffEntries()
  -> 利用側の LINQ Where + path pattern
```

## 4. 利用例

```csharp
ParallelDiffPathPattern ignoreLastEditingTime = ParallelDiffPathPattern.Parse(
    "Boards[*].Files[*].Document.Root.Children[*].Children[*].Attribute[LastEditingTime].Value");

ParallelDiffEntry[] effectiveDiffs = result
    .GetDiffEntries()
    .Where(entry => !entry.PathMatches(ignoreLastEditingTime))
    .ToArray();
```

元の一覧を残したまま分類する場合:

```csharp
var classified = result
    .GetDiffEntries()
    .Select(entry => new
    {
        Entry = entry,
        IsFiltered = entry.PathMatches(ignoreLastEditingTime),
    })
    .ToArray();
```

## 5. 公開 API

```csharp
public sealed class ParallelDiffPathPattern
{
    public static ParallelDiffPathPattern Parse(string pattern);

    public static bool TryParse(
        string pattern,
        out ParallelDiffPathPattern? parsedPattern);

    public bool IsMatch(string path);
}

public static class ParallelDiffEntryPathExtensions
{
    public static bool PathMatches(
        this ParallelDiffEntry entry,
        ParallelDiffPathPattern pattern);
}
```

### 5.1 例外と失敗

- `Parse(null)` は `ArgumentNullException`
- `Parse(不正な構文)` は `FormatException`
- `TryParse(null または不正な構文)` は `false`
- `IsMatch(null)` は `ArgumentNullException`
- `PathMatches(null entry, ...)` は `ArgumentNullException`
- `PathMatches(..., null pattern)` は `ArgumentNullException`
- 判定対象 path が既存 XPath-like path として不正な場合、`IsMatch` は `false`

## 6. パターン構文

既存 XPath-like path の root-relative grammar を基礎とし、selector に `[*]` を追加する。

```text
pattern          = segment *( "." segment )
segment          = member-name [ selector-pattern ]
selector-pattern = exact-selector / any-selector
any-selector     = "[*]"
```

`[*]` は、その segment に selector が存在する任意の child に一致する。

一致対象:

- key selector: `Boards[MainBoard]`
- numeric key selector: `Children[0]`
- ordinal selector: `Children[#0]`

一致しない対象:

- selector を持たない scalar/object member: `Document`
- member 名が異なる segment

初期実装では member 名の wildcard、任意深度 wildcard、prefix/subtree match は提供しない。path 全体の segment 数と各 segment が一致する exact match のみとする。

## 7. 一致規則

候補 path と pattern を segment 単位で比較する。

1. segment 数が異なる場合は不一致
2. member 名は `StringComparison.Ordinal` 相当で完全一致
3. pattern に selector がない場合、候補にも selectorがない場合だけ一致
4. pattern selector が `[*]` の場合、候補に key または ordinal selector があれば一致
5. exact key selector は key text と完全一致
6. exact ordinal selector は ordinal 値と一致
7. key selector と ordinal selector は相互に一致しない

例:

```text
Pattern: Boards[*].Files[*].Document.Root.Children[*].Attribute[LastEditingTime].Value

Match:
Boards[A].Files[No1/ygx].Document.Root.Children[0].Attribute[LastEditingTime].Value
Boards[B].Files[No2/ygx].Document.Root.Children[#3].Attribute[LastEditingTime].Value

No match:
Boards[A].Files[No1/ygx].Document.Root.Attribute[LastEditingTime].Value
Boards[A].Files[No1/ygx].Document.Root.Children[0].Attribute[CreatedAt].Value
```

## 8. 実装方針

- 候補 path の解析には既存 `XPathLikePathParser` を再利用する
- pattern parser は既存 grammar と escape 規則を維持しつつ `[*]` だけを追加解釈する
- 文字列の `StartsWith`、`Contains`、正規表現変換では判定しない
- `ParallelDiffEntry` 本体には `IsIgnored` 等の状態を追加しない
- `IEnumerable<ParallelDiffEntry>` を書き換える拡張は追加せず、標準 LINQ の `Where` と `PathMatches` を組み合わせる
- pattern は `Parse` 時に一度構造化し、各 `IsMatch` 呼び出しでは pattern の再解析を行わない

## 9. テスト方針

### 9.1 pattern parser

- exact member/key path を解析できる
- `[*]` を解析できる
- ordinal selector `[#0]` を exact selector として解析できる
- bracket 内の dot と既存 escape を維持する
- 空文字、未閉じ bracket、空 selector、二重 selector、不正 escape を拒否する

### 9.2 match

- 任意 board key に一致する
- 任意 file key に一致する
- `[*]` が key selector と ordinal selectorの両方に一致する
- exact key と exact ordinal を区別する
- member 名違い、segment 数違い、selector 有無違いを拒否する
- 不正な候補 path に対して `false` を返す

### 9.3 LINQ 利用

- `.Where(entry => !entry.PathMatches(pattern))` で対象差分だけ除外できる
- 元の `IReadOnlyList<ParallelDiffEntry>` の件数と内容は変化しない
- `CompareResult<T>.Issues` と `Root.HasDifferences()` は変化しない

### 9.4 `CompareIgnore` 回帰

- 既存 `CompareIgnore` テストを変更せず通す
- `[CompareIgnore]` 付き sequence が CompareKey 検証対象にならない既存契約を維持する
- 本機能の production code から `CompareIgnoreAttribute`、`TypeMetadataResolver`、`CompareConfiguration` を参照しない
