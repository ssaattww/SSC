# Diff Entry Path Filter

## 1. 目的

`CompareResult<T>.GetDiffEntries()` が返す差分一覧に対し、XPath-like path の構造を使って利用側で任意の差分を絞り込めるようにする。

pattern は一致した差分 path 自身だけでなく、その node 配下の子孫差分にも一致する。これにより、利用側は subtree の起点となる祖先 path を一つ指定して、子 node、属性、および値の差分をまとめて除外できる。

想定例:

```text
Pattern:
Boards[*].Files[*].Document.Root.Children[*]

一致対象:
Boards[Main].Files[No1/ygx].Document.Root.Children[0]
Boards[Main].Files[No1/ygx].Document.Root.Children[0].Children[1]
Boards[Main].Files[No1/ygx].Document.Root.Children[0].Attribute[LastEditingTime].Value
```

基板名、ファイル識別子、子要素の key または ordinal が入力ごとに変化しても、構造上同じ位置にある差分とその子孫を一致させる。

## 2. 非目的

- `[CompareIgnore]` の動作変更
- 比較対象メンバーの変更
- `ParallelCompareApi.Compare()` の比較結果変更
- `CompareConfiguration` への ignore path 設定追加
- `GetDiffEntries()` が返す差分一覧の変更
- `CompareResult<T>.Root`、`Issues`、`ValueState`、`HasDifferences()` の変更
- 正規表現による path 判定
- XPath の axis、predicate、再帰検索の実装
- member 名 wildcard または任意深度 wildcard の追加

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

特定属性の値だけを除外する完全一致 pattern:

```csharp
ParallelDiffPathPattern ignoreLastEditingTime = ParallelDiffPathPattern.Parse(
    "Boards[*].Files[*].Document.Root.Children[*].Children[*].Attribute[LastEditingTime].Value");

ParallelDiffEntry[] effectiveDiffs = result
    .GetDiffEntries()
    .Where(entry => !entry.PathMatches(ignoreLastEditingTime))
    .ToArray();
```

特定 node 配下をまとめて除外する祖先 pattern:

```csharp
ParallelDiffPathPattern ignoreChildren = ParallelDiffPathPattern.Parse(
    "Boards[*].Files[*].Document.Root.Children[*]");

ParallelDiffEntry[] effectiveDiffs = result
    .GetDiffEntries()
    .Where(entry => !entry.PathMatches(ignoreChildren))
    .ToArray();
```

元の一覧を残したまま分類する場合:

```csharp
var classified = result
    .GetDiffEntries()
    .Select(entry => new
    {
        Entry = entry,
        IsFiltered = entry.PathMatches(ignoreChildren),
    })
    .ToArray();
```

## 5. 公開 API

```csharp
public sealed class ParallelDiffPathPattern
{
    public static ParallelDiffPathPattern Parse(string pattern);

    public static bool TryParse(
        string? pattern,
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

既存 XPath-like path の root-relative grammar を基礎とし、selector に `[*]` と `*` の escape を追加する。

```text
pattern          = segment *( "." segment )
segment          = member-name [ selector-pattern ]
selector-pattern = exact-selector / any-selector / escaped-asterisk-selector
any-selector     = "[*]"
escaped-asterisk-selector = "[\\*]"
```

`[*]` は、その segment に selector が存在する任意の child に一致する。

`[\*]` は `*` をエスケープして通常文字の key として扱う。`[*]` の wildcard と区別される。既存の `\]`、`\\`、`\#` の escape 規則も維持する。

一致対象:

- key selector: `Boards[MainBoard]`
- numeric key selector: `Children[0]`
- ordinal selector: `Children[#0]`

一致しない対象:

- selector を要求する `Boards[*]` に対する selector なしの `Boards`
- member 名が異なる segment
- pattern より浅い候補 path

pattern が候補 path より短く、pattern の全 segment が先頭から一致する場合、残りの候補 segment は一致した node の子孫として扱う。

初期実装から引き続き、member 名 wildcard と任意深度 wildcard は提供しない。

## 7. 一致規則

候補 path と pattern を segment 単位で比較する。

1. 候補 path の segment 数が pattern より少ない場合は不一致
2. pattern の各 segment を候補 path の先頭から順に比較する
3. member 名は `StringComparison.Ordinal` 相当で完全一致
4. pattern に selector がない場合、候補の同じ segment にも selector がない場合だけ一致
5. pattern selector が `[*]` の場合、候補の同じ segment に key または ordinal selector があれば一致
6. exact key selector は key text と完全一致
7. exact ordinal selector は ordinal 値と一致
8. key selector と ordinal selector は相互に一致しない
9. pattern の全 segment が一致した後に候補側へ残る segment は子孫 path として許容する

例:

```text
Pattern: Root.A

Match:
Root.A
Root.A.B
Root.A.B.C
Root.A.Attribute[Width].Value

No match:
Root
Root.AA
Root.AA.B
```

`Root.A` と `Root.AA` は異なる member segment として解析されるため、文字列 prefix が同じでも一致しない。

selector を含む例:

```text
Pattern: Root.Children[*]

Match:
Root.Children[0]
Root.Children[#3].Value
Root.Children[Main].Attribute[Width].Value

No match:
Root.Children
Root.ChildrenOther[0].Value
```

## 8. 実装方針

- 候補 path の解析には既存 `XPathLikePathParser` を再利用する
- pattern parser は既存 grammar と escape 規則を維持しつつ `[*]` と `[\*]` を追加解釈する
- pattern と候補を解析済み segment 単位で比較する
- 文字列の `StartsWith`、`Contains`、正規表現変換では判定しない
- 候補 path が pattern 以上の深さであることを確認し、pattern segment 数だけ比較する
- `ParallelDiffEntry` 本体には `IsIgnored` 等の状態を追加しない
- `IEnumerable<ParallelDiffEntry>` を書き換える拡張は追加せず、標準 LINQ の `Where` と `PathMatches` を組み合わせる
- pattern は `Parse` 時に一度構造化し、各 `IsMatch` 呼び出しでは pattern の再解析を行わない

## 9. テスト方針

### 9.1 pattern parser

- exact member/key path を解析できる
- `[*]` を解析できる
- `[\*]` が `*` をエスケープして通常文字の key として扱う
- ordinal selector `[#0]` を exact selector として解析できる
- bracket 内の dot と既存の `\]`、`\\`、`\#` escape を維持する
- 空文字、未閉じ bracket、空 selector、二重 selector、不正 escape を拒否する
- `TryParse(null)` は `false` とし、`Parse(null)` と `IsMatch(null)` は `ArgumentNullException` とする

### 9.2 match

- pattern 自身への完全一致を維持する
- pattern より深い子 node、属性、値の path に一致する
- pattern より浅い候補 pathには一致しない
- `Root.A` が `Root.AA` と `Root.AA.B` に一致しない
- 任意 board key に一致する
- 任意 file key に一致する
- `[*]` が key selector と ordinal selectorの両方に一致する
- selector wildcard を含む祖先 pattern が子孫 path に一致する
- `[\*]` が `*` をエスケープして通常文字の key として扱い、他の key には一致しない
- exact key と exact ordinal を区別する
- member 名違いと selector 有無違いを拒否する
- 不正な候補 path に対して `false` を返す

### 9.3 LINQ 利用

- `.Where(entry => !entry.PathMatches(pattern))` で完全一致対象だけを除外できる
- 祖先 pattern で子 node、属性、値の子孫差分をまとめて除外できる
- 文字列 prefixだけが同じ兄弟 pathは残る
- 元の `IReadOnlyList<ParallelDiffEntry>` の件数と内容は変化しない
- `CompareResult<T>.Issues` と `Root.HasDifferences()` は変化しない
- `PathMatches(null entry, ...)` と `PathMatches(..., null pattern)` は `ArgumentNullException` とする

### 9.4 `CompareIgnore` 回帰

- 既存 `CompareIgnore` テストを変更せず通す
- `[CompareIgnore]` 付き sequence が CompareKey 検証対象にならない既存契約を維持する
- 本機能の production code から `CompareIgnoreAttribute`、`TypeMetadataResolver`、`CompareConfiguration` を参照しない

## 10. 互換性

- public API shape は変更しない
- pattern 自身への完全一致、selector、escape、例外の既存契約を維持する
- pattern より浅いpathと異なるsegmentは引き続き不一致
- 変更点は、従来不一致だった「patternが祖先となる子孫path」を一致として扱うこと
- repository内の既存利用例は完全な差分pathを指定しており、同じ差分への一致結果は維持される
- 外部利用者が子孫pathを意図的に不一致として扱っていた場合は結果が変わるため、behavior変更として `Design/BreakingChanges.md` に記録する
