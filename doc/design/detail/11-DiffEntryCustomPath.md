# 差分 entry の利用側定義 path 投影（Diff Entry Custom Path Projection）

## 1. 目的

`CompareResult<T>.GetDiffEntries()` が返す差分 entry に対し、SSC が生成する既存 path を変更せず、利用側が用途に応じた別の path 表現を生成できるようにする。

再帰的な model では、既存 path が次のように同じ member 名を繰り返すことがある。

```text
Root.Children[0].Children[0].Fields[0].Value
```

この path は SSC の比較 tree 上の位置を正しく表している。一方、レポートを読む利用者にとっては、各 node が持つ `Name` のような実際の値を使った次の表現の方が分かりやすい場合がある。

```text
Root.Child1[0].Child2[0].Attribute1[0].Value
```

本機能は、標準の差分情報を保持したまま別の見え方を追加する。

```text
標準 path:
Root.Children[0].Children[0].Fields[0].Value

利用側定義 path:
Root.Child1[0].Child2[0].Attribute1[0].Value
```

本機能は比較完了後の表示、分類、絞り込みを改善するための機能である。比較対象、比較結果、container 要素の対応付けには影響しない。

## 2. 用語

本設計では、英語の用語だけを記載して意味を省略しない。次の意味で用語を使用する。

| 用語 | 本設計での意味 |
|---|---|
| entry | 差分1件を表す `ParallelDiffEntry`、または利用側定義 path を付加した結果1件 |
| model | `ParallelCompareApi.Compare()` へ渡す比較対象 object |
| model slot | 比較に渡した各 model の位置。`modelIndex` と同じ意味 |
| node | SSC の比較 tree を構成する `IParallelNode` 1件 |
| tree | 親子関係で構成される階層構造 |
| member | C# model の property または field |
| container | `List`、array、`IEnumerable`、dictionary など、複数要素を保持する member |
| sequence | 順序を持つ container の要素列 |
| key | container 要素を対応付ける識別値 |
| ordinal | key を持たない要素を並び順で識別するときの0始まりの番号 |
| standard path / canonical path | SSC が `ParallelDiffEntry.Path` に格納する既存の正式 path。本設計では「標準 path」と呼ぶ |
| custom path | 利用側が標準 path から生成する別表現。本設計では「利用側定義 path」と呼ぶ |
| alias | 同じ対象へ付ける別名 |
| projection | 元の情報を保持したまま別の見え方へ変換する処理。本設計では「投影」と呼ぶ |
| projector | 投影規則を実装する利用側 component。本設計では「投影器」と呼ぶ |
| segment | dotで区切られた path の構成要素1件 |
| selector | container 要素を識別する `[]` 部分 |
| context | 投影器の判断材料として SSC が渡す現在位置の情報。本設計では「文脈情報」と呼ぶ |
| sibling | 同じ親を持つ兄弟 node |
| ancestor | 現在位置より上位にある祖先 node |
| runtime value | 比較実行時に node の各 model slot が保持している実際の値 |
| fallback | 利用側定義名を決定できない場合に、安全な既定動作へ戻すこと |
| pattern | path を照合するためのひな形 |
| wildcard | 任意の値へ一致する記号。既存 API では selector の `[*]` |
| formatter | 構造化された segment を path 文字列へ変換する処理 |
| immutable | 作成後に内容を書き換えない性質。本設計では文脈情報の外部書き換えを許可しない |
| deterministic | 同じ入力に対して同じ結果を返す性質。本設計では投影器の推奨実装方針 |
| pipeline | 入力 model から比較結果を生成する一連の処理 |

### 2.1 標準 path（canonical path）

`canonical` は「基準となる正式な表現」という意味である。

本設計での標準 path は、SSC が現在 `ParallelDiffEntry.Path` に格納している正式な path を指す。

例:

```text
Groups[1].Items[100].MetricA
Root.Children[0].Fields[0].Value
Items[#0].Name
```

標準 path は SSC の比較 tree 上の位置を表す。

- `Kind == Node` の entry では `GetNodeByPath()` で同じ node を取得できる
- 空文字列のCompareKeyによる既存base互換の`Name[]`形式は文字列を維持するlegacy selectorであり、通常のparserでは解釈できないためnodeとparent pathの解決を保証しない。ただし共有matcherの候補pathとしては空 key selector として扱われるため、`Name[*]` はこの形式の子孫pathに一致する
- `Kind == ContainerPresence` の entry は public node に対応しないため、`GetNodeByPath()` による解決を保証しない
- `ParallelDiffEntry.Path` と `ParallelDiffEntry.ParentPath` に使用される
- 既存 `ParallelDiffEntry.PathMatches()` の照合対象になる
- `ParallelDiffEntry.ToString()` の表示に使用される
- 既存 API の後方互換対象である

利用側定義 path を導入しても、標準 path の意味と文字列は変更しない。

### 2.2 利用側定義 path（custom path）

`custom` は「利用側が用途に合わせて定義する」という意味である。

利用側定義 path は、利用側が標準 path を別の意味表現へ変換した path である。

例:

```text
標準 path:
Root.Children[0].Fields[0].Value

利用側定義 path:
Root.Child1[0].Attribute1[0].Value
```

利用側定義 path は次の用途で使用する。

- レポート表示
- ログ表示
- 差分の分類
- `ParallelDiffPathPattern` による絞り込み

初期実装では node の住所として扱わない。

```csharp
result.GetNodeByPath(projection.Entry.Path);       // 標準 path なので解決対象
result.GetNodeByPath(projection.ProjectedPath);   // 解決を保証しない
```

### 2.3 Alias と projection

`alias` は「同じ対象へ付ける別の名前」を意味する。

例えば、標準 segment `Children[0]` を `Child1[0]` と表示する場合、`Child1` は別名に相当する。

ただし、今回の機能は固定文字列への単純な名前変更だけではない。

- node の runtime value を参照できる
- segment を維持できる
- segment を置き換えられる
- segment を省略できる
- model slot 間の値を利用側規則で判定できる

そのため、公開 API の中心用語には `alias` より広い意味を持つ `projection` を採用する。

### 2.4 Segment

`segment` は、dotで区切られた path の構成要素1件を指す。

```text
Root.Children[0].Value
```

この path は次の3 segment で構成される。

```text
Root
Children[0]
Value
```

### 2.5 Selector

`selector` は、container 要素を識別する `[]` 部分を指す。

```text
Items[A]
Items[0]
Items[#0]
```

本設計では selector を次の2種類に分ける。

| 表現 | 種類 | 意味 |
|---|---|---|
| `[A]` | key selector | key text `A` で要素を識別する |
| `[0]` | key selector | numeric text `0` を key として要素を識別する |
| `[#0]` | ordinal selector | key を持たない要素を並び順 `0` で識別する |

`[0]` と `[#0]` は見た目が近いが、SSC path grammar 上の意味は異なる。

既存比較結果では、index alignment に由来する要素が `Children[0]` のように出力される場合がある。本機能は既存の selector を再解釈せず、その種類と文字列を維持する。

```text
標準 segment:     Children[0]
利用側定義 segment: Child1[0]
```

標準 segment が ordinal selector を持つ場合も同様に維持する。

```text
標準 segment:     Children[#0]
利用側定義 segment: Child1[#0]
```

具体的な path segment では wildcard `[*]` を使用しない。`[*]` は `ParallelDiffPathPattern` 側だけの表現とする。

### 2.6 Context

`context` は「現在どの segment を投影しているか」と、その判断に必要な node 情報をまとめた文脈情報を指す。

例えば、次の標準 path の `Children[0]` を投影しているとする。

```text
Root.Children[0].Value
     ^^^^^^^^^^^
```

文脈情報には概ね次が含まれる。

```text
現在の標準 segment: Children[0]
親 node:             Root node
現在 node:           Children[0] が指す node
兄弟 node:           同じ Children child set に属する node 一覧
祖先情報:            Root segment の文脈情報
```

SSC はこの情報を既に比較 tree の走査中に把握しているため、利用側へ安全に公開する。

### 2.7 Fallback

`fallback` は、利用側が別名を決定できない場合に安全な既定動作へ戻すことである。

本設計では `KeepStandard()` による標準 segment の維持を推奨する。

```text
Children[0]
    ↓ runtime value が model slot 間で一致しない
Children[0]
```

## 3. 現状と問題

現行 `GetDiffEntries()` は、比較 tree を再帰的に走査し、member 名と selector を連結して `ParallelDiffEntry.Path` を生成する。

この path は比較 tree の位置として正しい。しかし、同じ型を再帰的に保持する model では、利用者にとって意味の薄い member 名が繰り返される。

例として次の model を考える。

```csharp
public sealed class Document
{
    public TreeNode Root { get; init; } = new();
}

public sealed class TreeNode
{
    public string Name { get; init; } = string.Empty;

    public List<TreeNode> Children { get; init; } = [];

    public List<NamedValue> Fields { get; init; } = [];
}

public sealed class NamedValue
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
```

実データが次の場合を考える。

```text
Root
└─ Child1
   └─ Child2
      └─ Attribute1 = "0"
```

標準 path は次のようになる。

```text
Root.Children[0].Children[0].Fields[0].Value
```

利用者がレポートで読みたい path は次のような表現である。

```text
Root.Child1[0].Child2[0].Attribute1[0].Value
```

SSC が `Name` を自動採用することはできない。

- `Name` が path 名であるとは限らない
- 別 model では `Code`、`Id`、`Label` が意味名かもしれない
- model slot 間で名前が異なる場合の採用規則は利用側ごとに異なる
- 同名 sibling の区別規則も利用側ごとに異なる
- `Value` segment を表示するか省略するかも利用側ごとに異なる

SSC は判断材料だけを渡し、意味の決定を利用側へ委ねる必要がある。

## 4. 責務境界

### 4.1 SSC の責務

SSC は次を担当する。

1. 既存の標準 path を変更せず生成する
2. 標準 path を構造化された segment と selector として扱う
3. path 生成時に把握している node の文脈情報を投影器へ渡す
4. 投影器が返した具体的 segment を標準 path と同じ grammar で文字列化する
5. 標準 entry と利用側定義 path を同じ結果から参照できるようにする
6. 利用側定義 path に既存 `ParallelDiffPathPattern` を適用できるようにする
7. key text の escape を標準 path と利用側定義 path で共通化する
8. 既存 `GetDiffEntries()` の件数、順序、path、node 参照を維持する
9. 不正な segment から不正な path を生成しない

### 4.2 利用側の責務

利用側は次を担当する。

1. どの member を置換または省略するか決める
2. node のどの runtime value を利用側定義名として使うか決める
3. model slot 間で候補名が異なる場合の規則を決める
4. 同名 sibling をどう区別するか決める
5. 名前を決定できない場合の fallback を決める
6. 利用側定義 path をレポート、ログ、分類へどう表示するか決める
7. 投影器を同じ入力に対して同じ結果を返す実装にする

### 4.3 SSC が解釈しない情報

SSC は次のような model 固有の意味を自動判定しない。

```text
Name      = node 名
Children  = XML の子要素
Fields    = 属性
Value     = レポートでは省略可能
```

SSC production code に XML、JSON、YAML、DOM 等の domain 固有規則を入れない。

## 5. 非目的

初期実装では次を行わない。

- `ParallelDiffEntry.Path` の意味変更
- `ParallelDiffEntry.ParentPath` の意味変更
- 利用側定義 path を使う `GetNodeByPath()` 相当 API
- `CompareConfiguration` への投影器設定追加
- 比較対象 member の変更
- `CompareKey` の代替
- sequence alignment の変更
- `CompareIssue.Path` の投影
- XML、JSON、tree model 固有の投影器実装
- property 名から `Name` や `Id` を自動発見する規則
- 利用側定義 path の一意性保証
- member 名 wildcard や任意深度 wildcard の追加
- selector 種類の自動変換
- `[0]` と `[#0]` の相互変換
- 正規表現による path 変換
- 非同期 projector API
- projector の並列呼び出し

`CompareConfiguration` に投影器を置かない理由は、同じ比較結果へ複数の見え方を適用できるようにするためである。

```csharp
var reportPaths = result.GetDiffEntryPathProjections(reportProjector);
var logPaths = result.GetDiffEntryPathProjections(logProjector);
```

投影は比較設定ではなく、比較後の出力表現である。

## 6. 固定 alias attribute を対象外とする理由

### 6.1 Alias attribute とは

C# の attribute は、class、property、field などへ付加する metadata である。

固定 alias attribute を導入する場合、概念的には次の API になる。

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class DiffPathAliasAttribute : Attribute
{
    public DiffPathAliasAttribute(string alias)
    {
        Alias = alias;
    }

    public string Alias { get; }
}
```

利用例:

```csharp
public sealed class Order
{
    [DiffPathAlias("Lines")]
    public List<OrderLine> Items { get; init; } = [];
}
```

この仕組みなら固定置換は可能である。

```text
Items[100].Price
    ↓
Lines[100].Price
```

### 6.2 今回の要求を満たせない理由

再帰 model で必要になる名前は、property に固定された名前ではなく、各 node の runtime value である。

```csharp
public sealed class TreeNode
{
    public string Name { get; init; } = string.Empty;

    public List<TreeNode> Children { get; init; } = [];
}
```

同じ `Children` property の要素でも、実際の名前は異なる。

```text
Children[0] -> Child1[0]
Children[1] -> Child2[1]
Children[2] -> Child3[2]
```

property に次のような固定 attribute を付けても、要素ごとの名前を表現できない。

```csharp
[DiffPathAlias("Child1")]
public List<TreeNode> Children { get; init; } = [];
```

さらに attribute 方式では metadata resolution へ責務が広がる。

- `TypeMetadataResolver` の変更が必要になる
- comparison pipeline と出力表現が結合する
- 同じ比較結果へ複数の表示規則を適用しにくい
- runtime value を参照できない

したがって初期実装では固定 alias attribute を追加しない。

## 7. 公開 API

### 7.1 投影を取得する入口

```csharp
public static class ParallelPathAccessExtensions
{
    public static IReadOnlyList<ParallelDiffEntry> GetDiffEntries<T>(
        this CompareResult<T> result);

    public static IReadOnlyList<ParallelDiffEntryPathProjection>
        GetDiffEntryPathProjections<T>(
            this CompareResult<T> result,
            IParallelDiffPathProjector projector);
}
```

既存 `GetDiffEntries()` は変更しない。

`GetDiffEntryPathProjections()` は、標準差分 entry と利用側定義 path の組を返す。

比較結果に root node がない場合は空一覧を返す。

### 7.2 投影器

```csharp
public interface IParallelDiffPathProjector
{
    ParallelDiffPathSegmentProjection Project(
        ParallelDiffPathProjectionContext context);
}
```

`Project()` は標準 path の segment 1件に対し、利用側定義 path での扱いを返す。

投影器は同期的に呼び出す。

同じ segment が複数の差分 entry に含まれる場合、投影器は同じ node に対して複数回呼ばれる可能性がある。投影器は呼び出し回数へ依存しない実装とする。

### 7.3 投影文脈

```csharp
public sealed class ParallelDiffPathProjectionContext
{
    public IReadOnlyList<ParallelDiffPathNodeContext> Ancestors { get; }

    public ParallelDiffPathNodeContext Current { get; }
}
```

`Ancestors` は root 側から現在の親までの順序で保持する。現在位置自身は含めず、`Current` で参照する。

root 直下 segment では `Ancestors.Count == 0` になる。

```csharp
public sealed class ParallelDiffPathNodeContext
{
    public ParallelDiffPathSegment StandardSegment { get; }

    public IParallelNode ParentNode { get; }

    public IParallelNode? Node { get; }

    public IReadOnlyList<IParallelNode> Siblings { get; }
}
```

各 property の意味は次のとおり。

| Property | 内容 |
|---|---|
| `StandardSegment` | SSC が生成する標準 path segment |
| `ParentNode` | `StandardSegment` を所有する親 node |
| `Node` | segment が指す現在 node。container presence entry では `null` |
| `Siblings` | 同じ `ParallelChildSet` に属する node 一覧。container presence entry では空 |

`Ancestors` と `Siblings` は外部から書き換えられない読み取り専用 snapshot とする。

`snapshot` は、ある時点の内容を固定した写しを意味する。

### 7.4 Path segment

```csharp
public sealed class ParallelDiffPathSegment
{
    public string MemberName { get; }

    public ParallelDiffPathSelector? Selector { get; }

    public static ParallelDiffPathSegment Member(string memberName);

    public static ParallelDiffPathSegment Key(
        string memberName,
        string keyText);

    public static ParallelDiffPathSegment Ordinal(
        string memberName,
        int ordinal);

    public ParallelDiffPathSegment WithMemberName(string memberName);
}
```

`WithMemberName()` は selector を維持し、member 名だけを変更する。

例:

```csharp
ParallelDiffPathSegment standard =
    ParallelDiffPathSegment.Key("Children", "0");

ParallelDiffPathSegment custom =
    standard.WithMemberName("Child1");
```

結果:

```text
Children[0]
    ↓
Child1[0]
```

ordinal selector の場合も種類を維持する。

```csharp
ParallelDiffPathSegment standard =
    ParallelDiffPathSegment.Ordinal("Children", 0);

ParallelDiffPathSegment custom =
    standard.WithMemberName("Child1");
```

結果:

```text
Children[#0]
    ↓
Child1[#0]
```

### 7.5 Selector

```csharp
public enum ParallelDiffPathSelectorKind
{
    Key,
    Ordinal,
}
```

```csharp
public readonly struct ParallelDiffPathSelector
{
    public ParallelDiffPathSelectorKind Kind { get; }

    public string? KeyText { get; }

    public int? Ordinal { get; }
}
```

path segment factory が selector の整合性を保証する。

- `Key()` は非null・非空の `KeyText` を持つ
- `Ordinal()` は非負の `Ordinal` を持つ
- wildcard は保持しない
- 利用側が selector の不整合な組を直接生成する public constructor は提供しない

### 7.6 Segment の投影結果

```csharp
public enum ParallelDiffPathSegmentProjectionKind
{
    KeepStandard,
    Replace,
    Omit,
}
```

```csharp
public readonly struct ParallelDiffPathSegmentProjection
{
    public ParallelDiffPathSegmentProjectionKind Kind { get; }

    public ParallelDiffPathSegment? Replacement { get; }

    public static ParallelDiffPathSegmentProjection KeepStandard();

    public static ParallelDiffPathSegmentProjection Replace(
        ParallelDiffPathSegment segment);

    public static ParallelDiffPathSegmentProjection Omit();
}
```

#### `KeepStandard`

標準 segment をそのまま利用する。

```text
Value
    ↓
Value
```

#### `Replace`

指定した具体的 segment へ置き換える。

```text
Children[0]
    ↓
Child1[0]
```

置換先 segment では member 名だけでなく selector も変更または省略できる。

```text
Items[A]
    ↓
Item
```

この場合、複数 entry が同じ利用側定義 path になる可能性がある。一意性は保証しない。

#### `Omit`

利用側定義 path から segment を省略する。

```text
DocumentWrapper.Root.Children[0]
    ↓ DocumentWrapper を省略
Root.Children[0]
```

末尾 segment も省略できる。

```text
Root.Child1[0].Value
    ↓ Value を省略
Root.Child1[0]
```

### 7.7 投影結果

```csharp
public sealed class ParallelDiffEntryPathProjection
{
    public ParallelDiffEntry Entry { get; }

    public string ProjectedPath { get; }

    public string? ProjectedParentPath { get; }
}
```

各 property の意味は次のとおり。

| Property | 内容 |
|---|---|
| `Entry` | 既存の標準差分 entry |
| `Entry.Path` | SSC が生成した標準 path |
| `Entry.ParentPath` | SSC が生成した標準 parent path |
| `ProjectedPath` | 投影器を適用した利用側定義 path |
| `ProjectedParentPath` | 標準 parent path と同じ segment 範囲へ投影器を適用した path |

利用例:

```csharp
ParallelDiffEntryPathProjection projected = projections[0];

string standardPath = projected.Entry.Path;
string customPath = projected.ProjectedPath;
```

結果例:

```text
standardPath:
Root.Children[0].Fields[0].Value

customPath:
Root.Child1[0].Attribute1[0].Value
```

### 7.8 Pattern matching

```csharp
public static class ParallelDiffEntryPathProjectionExtensions
{
    public static bool PathMatches(
        this ParallelDiffEntryPathProjection projection,
        ParallelDiffPathPattern pattern);
}
```

この overload は `ProjectedPath` を照合する。

Issue #48以降は完全一致に加え、patternの全segmentが `ProjectedPath` の先頭から一致する場合も、残りのsegmentを子孫pathとして許容する。

```text
Pattern:       Root.Child1[*]
ProjectedPath: Root.Child1[0].Attribute1[0].Value
Result:        match
```

member名とselectorはsegment単位で比較するため、文字列prefixが似ている別segmentには一致しない。

```text
Pattern:       Root.Child1[*]
ProjectedPath: Root.Child10[0].Attribute1[0].Value
Result:        no match
```

既存 overload は変更しない。

```csharp
entry.PathMatches(pattern);
```

既存 overload は引き続き `ParallelDiffEntry.Path`、つまり標準 path を照合する。投影用overloadの呼び出しによって標準pathの文字列または判定対象が利用側定義pathへ置き換わることはない。

## 8. 投影規則

### 8.1 標準 entry の維持

`GetDiffEntryPathProjections()` が返す `Entry` は、既存 `GetDiffEntries()` が返す entry と同じ標準情報を持つ。

次を変更しない。

- entry 件数
- entry 順序
- `Entry.Path`
- `Entry.ParentPath`
- `Entry.Kind`
- `Entry.ParentNode`
- `Entry.Node`
- `Entry.Values`
- `Entry.ToString()`

`Kind == Node` の標準 node path は、空文字列のCompareKeyによる既存base互換の`Name[]`形式を除き、引き続き解決できる。

```csharp
projection.Entry.Kind == ParallelDiffEntryKind.Node
    && projection.Entry.Node == result.GetNodeByPath(projection.Entry.Path);
```

利用側定義 path にはこの契約を適用しない。

### 8.2 Segment 単位の処理

標準 path を root 側から segment 単位で処理する。

```text
Root
Children[0]
Fields[0]
Value
```

各 segment に対して投影器を呼び出し、結果を順番に連結する。

### 8.3 Parent path

`ProjectedParentPath` は、標準 `ParentPath` と同じ segment 範囲へ投影器を適用して生成する。
root 直下の entry に加え、標準 parent path の範囲にあるすべての segment を `Omit()` した場合も `null` とする。

標準 path:

```text
Root.Children[0].Value
```

標準 parent path:

```text
Root.Children[0]
```

利用側定義 path:

```text
Root.Child1[0].Value
```

利用側定義 parent path:

```text
Root.Child1[0]
```

末尾 segment を省略した場合、`ProjectedPath` と `ProjectedParentPath` が同じ文字列になることを許容する。

```text
標準 path:             Root.Children[0].Value
標準 parent path:      Root.Children[0]
利用側定義 path:       Root.Child1[0]
利用側定義 parent path: Root.Child1[0]
```

利用側定義 path は node lookup 用の住所ではないため、同じ文字列でも矛盾しない。

### 8.4 Escape

`escape` は、path grammar で特別な意味を持つ文字を通常文字として表現できるように変換することである。

利用側が返す key text は、SSC が既存 path grammar に従って escape する。

```text
key text: A]B
path:     Items[A\]B]

key text: A\B
path:     Items[A\\B]

key text: #0
path:     Items[\#0]
```

利用側は key text を文字列連結して path へ埋め込まない。

```csharp
ParallelDiffPathSegment.Key("Items", keyText);
```

member 名では現行 grammar に escape 規則がないため、次の文字を含む member 名を拒否する。

```text
.
[
]
```

不正な member 名から暗黙に不正 path を生成しない。

### 8.5 Selector の維持

member 名だけを変更する場合は `WithMemberName()` を使用し、selector の種類と値を維持する。

```text
Children[0]
    ↓
Child1[0]
```

```text
Children[#0]
    ↓
Child1[#0]
```

次の自動変換は行わない。

```text
[0]  -> [#0]
[#0] -> [0]
```

### 8.6 空 path の拒否

すべての segment が `Omit()` され、`ProjectedPath` が空になる場合は `InvalidOperationException` とする。

空文字を有効な利用側定義 path として返さない。

### 8.7 重複 path

複数 entry が同じ `ProjectedPath` になることを許容する。

例:

```text
標準 path 1: Items[A].Name
標準 path 2: Items[B].Name

利用側定義 path 1: Item.Name
利用側定義 path 2: Item.Name
```

SSC は利用側定義 path の一意性を保証しない。

理由:

- 投影は表示と絞り込みのための機能である
- entry は `Entry.Path` により標準位置を保持している
- 同名 node の区別規則は利用側固有である

pattern は一致したすべての entry を返す。

### 8.8 Model slot 間の値

SSC は複数 model slot のどの値を利用側定義名として採用するか決めない。

投影器は `IParallelNode.Count`、`GetValue()`、`GetState()` を使って判断する。

推奨例:

1. `Missing` ではない slot から候補名を取得する
2. 候補名がすべて同じ場合だけその名前を使う
3. 候補名が異なる場合は `KeepStandard()` へ fallback する
4. 候補名がnull、空、空白の場合も `KeepStandard()` へ fallback する

この規則自体は SSC production code へ実装しない。

### 8.9 Container presence entry

`container presence entry` は、empty container と missing container など、container 自体の存在状態の差分を表す entry である。

対応する要素 node が存在しない場合の文脈情報は次とする。

```text
Current.Node       = null
Current.Siblings   = empty
Current.ParentNode = container owner node
```

投影器は node value を必要とする置換を行えないため、通常は `KeepStandard()` を返す。

### 8.10 投影器の例外

投影器が送出した例外は握りつぶさず、呼び出し元へそのまま伝播する。

利用側規則の失敗を SSC が標準 segment へ暗黙 fallback しない。

## 9. 利用例

### 9.1 再帰 model

```csharp
public sealed class Document
{
    public TreeNode Root { get; init; } = new();
}

public sealed class TreeNode
{
    public string Name { get; init; } = string.Empty;

    public List<TreeNode> Children { get; init; } = [];

    public List<NamedValue> Fields { get; init; } = [];
}

public sealed class NamedValue
{
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
```

標準 path:

```text
Root.Children[0].Children[0].Fields[0].Value
```

利用側が目指す path:

```text
Root.Child1[0].Child2[0].Attribute1[0].Value
```

### 9.2 利用側投影器の概念例

次のコードは利用方法を示す例であり、SSC が domain 共通 projector として提供するものではない。

```csharp
public sealed class NamedTreePathProjector : IParallelDiffPathProjector
{
    public ParallelDiffPathSegmentProjection Project(
        ParallelDiffPathProjectionContext context)
    {
        ParallelDiffPathNodeContext current = context.Current;

        if (current.Node is null)
        {
            return ParallelDiffPathSegmentProjection.KeepStandard();
        }

        if (current.StandardSegment.MemberName == "Children")
        {
            string? name = TryGetCommonTreeNodeName(current.Node);
            return name is null
                ? ParallelDiffPathSegmentProjection.KeepStandard()
                : ParallelDiffPathSegmentProjection.Replace(
                    current.StandardSegment.WithMemberName(name));
        }

        if (current.StandardSegment.MemberName == "Fields")
        {
            string? name = TryGetCommonNamedValueName(current.Node);
            return name is null
                ? ParallelDiffPathSegmentProjection.KeepStandard()
                : ParallelDiffPathSegmentProjection.Replace(
                    current.StandardSegment.WithMemberName(name));
        }

        return ParallelDiffPathSegmentProjection.KeepStandard();
    }
}
```

`TryGetCommonTreeNodeName()` と `TryGetCommonNamedValueName()` は利用側が実装する。

SSC は次を知らない。

- `TreeNode.Name` が path 名であること
- `NamedValue.Name` が field 名であること
- model slot 間で名前が一致すべきこと
- 名前が異なる場合にどちらを採用すべきか

### 9.3 利用側定義 path の取得

```csharp
IReadOnlyList<ParallelDiffEntryPathProjection> projections =
    result.GetDiffEntryPathProjections(
        new NamedTreePathProjector());
```

標準 path と利用側定義 path を同時に参照できる。

```csharp
foreach (ParallelDiffEntryPathProjection projection in projections)
{
    Console.WriteLine($"Standard:  {projection.Entry.Path}");
    Console.WriteLine($"Projected: {projection.ProjectedPath}");
}
```

出力例:

```text
Standard:  Root.Children[0].Children[0].Fields[0].Value
Projected: Root.Child1[0].Child2[0].Attribute1[0].Value
```

### 9.4 Patternによる絞り込み

特定leafまで指定する完全一致pattern:

```csharp
ParallelDiffPathPattern exactPattern = ParallelDiffPathPattern.Parse(
    "Root.Child1[*].Child2[*].Attribute1[*].Value");
```

祖先node配下をまとめて対象とするpattern:

```csharp
ParallelDiffPathPattern subtreePattern = ParallelDiffPathPattern.Parse(
    "Root.Child1[*].Child2[*]");

ParallelDiffEntryPathProjection[] matched = result
    .GetDiffEntryPathProjections(new NamedTreePathProjector())
    .Where(projection => projection.PathMatches(subtreePattern))
    .ToArray();
```

`subtreePattern` は `Root.Child1[0].Child2[0]` 自身に加え、`Attribute1[0].Value` などの子孫segmentを持つ利用側定義pathにも一致する。

標準 path に対する既存 filter も同時に利用できる。

```csharp
ParallelDiffEntry[] standardMatches = result
    .GetDiffEntries()
    .Where(entry => entry.PathMatches(standardPattern))
    .ToArray();
```

### 9.5 Model slot 間の名前が異なる場合

左 model:

```text
Child2
```

右 model:

```text
ChildX
```

利用側投影器が「全slotで同じ名前の場合だけ置換する」規則なら、該当 segment は標準名へ fallback する。

```text
標準 path:
Root.Children[0].Children[0].Fields[0].Value

利用側定義 path:
Root.Child1[0].Children[0].Attribute1[0].Value
```

SSC は `Child2` と `ChildX` のどちらかを自動選択しない。

## 10. 例外と失敗

### 10.1 Entry projection

- `GetDiffEntryPathProjections(null result, ...)` は `ArgumentNullException`
- `GetDiffEntryPathProjections(..., null projector)` は `ArgumentNullException`
- 比較結果に root がない場合は空一覧
- 全segmentを省略した場合は `InvalidOperationException`
- 投影器が送出した例外はそのまま伝播

### 10.2 Segment factory

- `Member(null)` は `ArgumentNullException`
- `Member(空文字)` は `ArgumentException`
- `Member("A.B")` は `ArgumentException`
- `Member("A[B")` は `ArgumentException`
- `Member("A]B")` は `ArgumentException`
- `Key(..., null)` は `ArgumentNullException`
- `Key(..., 空文字)` は `ArgumentException`
- `Ordinal(..., 負数)` は `ArgumentOutOfRangeException`
- `Replace(null)` は `ArgumentNullException`

`validation` は、入力値が API 契約を満たすか検査することを意味する。

## 11. 実装方針

### 11.1 標準 path の構造化

従来の `GetDiffEntries()` は再帰処理の引数として文字列 path を連結していた。

本実装では、再帰位置までの標準 segment と node 文脈を内部 stack へ保持する。

`stack` は、現在の再帰位置までの要素を順番に積み上げる構造を意味する。

概念:

```text
Root
Root + Children[0]
Root + Children[0] + Fields[0]
Root + Children[0] + Fields[0] + Value
```

各 stack frame は次を保持する。

```text
StandardSegment
ParentNode
Node
Siblings
```

標準 path 文字列は frame の segment を formatter で連結して生成する。

既存 path の文字列結果は変更しない。

### 11.2 文脈情報の生成

差分 entry を生成する時点で、内部 stack を root 側から順に `ParallelDiffPathNodeContext` へ変換する。

各segmentの投影時には次を渡す。

```text
Ancestors = 現在より前の node context 一覧
Current   = 現在の node context
```

標準 path 文字列を再解析しない。

`GetNodeByPath()` で root から node を再探索しない。

### 11.3 Formatter の共有

標準 path と利用側定義 path は `ParallelDiffPathFormatter` を共有する。

次の処理を共通化する。

- dot連結
- `[]` の生成
- key text の escape
- 先頭 `#` の escape
- ordinal の `#` 表記

標準 path と利用側定義 path で異なる grammar を作らない。

### 11.4 Pattern parser の再利用

利用側定義 path は既存 path grammar で文字列化するため、`ParallelDiffPathPattern` の既存 parser と matcher を再利用する。

Issue #48の祖先一致規則も同じmatcherを経由して適用する。利用側定義path専用の別matcherまたは別のprefix判定は追加しない。

新しい wildcard grammar は追加しない。

### 11.5 Comparison pipeline との分離

本機能の production code から次を変更しない。

- `ParallelCompareApi.Compare()`
- `CompareConfiguration`
- `CompareIgnoreAttribute`
- `CompareKeyAttribute`
- `TypeMetadataResolver`
- container normalization
- key union
- sequence alignment
- node の value/state 判定

本機能は比較結果の列挙時にだけ動作する。

```text
models
  -> metadata resolution
  -> comparison tree construction
  -> CompareResult<T>
  -> GetDiffEntries()                        標準 entry
  -> GetDiffEntryPathProjections(projector) 標準 entry + 利用側定義 path
```

## 12. 後方互換

### 12.1 既存 API

次の API のsignatureは変更しない。

`signature` は、method名、引数、戻り値からなる公開形式を意味する。

```csharp
result.GetDiffEntries();
entry.PathMatches(pattern);
projection.PathMatches(pattern);
result.GetNodeByPath(path);
result.GetValueByPath(path, modelIndex);
result.GetStateByPath(path, modelIndex);
```

Issue #48では2つの `PathMatches` overloadに同じ祖先一致規則を適用する。従来不一致だった子孫pathが一致するため、標準pathまたは利用側定義pathで子孫を意図的に残していたfilter結果は変化し得る。

### 12.2 標準 path

次を維持する。

- 標準 path の文字列
- key selector と ordinal selector の区別
- key text の escape
- entry の列挙順
- parent path
- node 参照

空文字列のCompareKeyによる既存base互換の`Name[]`形式も標準path文字列として維持する。この形式は新しいgrammarではなく既存parserで解釈できないlegacy selectorであるため、nodeおよびparent pathのlookup保証には含めない。

### 12.3 `ParallelDiffEntry.ToString()`

既存どおり `ParallelDiffEntry.Path`、つまり標準 path を表示する。

利用側定義 path を暗黙に使用しない。

### 12.4 標準 path の解決可能性

`Kind == Node` の entry について、空文字列のCompareKeyによる既存base互換の`Name[]`形式を除き、次の既存契約を維持する。`Kind == ContainerPresence` は public node を持たないため、この解決可能性を保証しない。`Name[]`は文字列互換を優先する既存parser非対応のlegacy selectorであり、`Path`と`ParentPath`のlookupを保証しない。

```csharp
entry.Kind == ParallelDiffEntryKind.Node
    && entry.Node == result.GetNodeByPath(entry.Path);
```

利用側定義 path にはこの契約を適用しない。

## 13. テスト方針と実装済み回帰

本機能はテストを先に追加し、未実装 API による失敗を確認してから production code を追加する TDD で実装する。

`TDD` は Test-Driven Development の略であり、先に失敗するテストで期待動作を固定し、そのテストを満たす最小実装を追加する開発方法を意味する。

### 13.1 Segmentとselector

- member segment を生成できる
- key selector を生成できる
- ordinal selector を生成できる
- `WithMemberName()` が selector を維持する
- null、空、不正文字を拒否する
- empty key を拒否する
- negative ordinal を拒否する

### 13.2 Segment projection

- `KeepStandard()` が標準 segment を維持する
- `Replace()` が member 名を変更できる
- `Replace()` が key selector を維持できる
- `Replace()` が ordinal selector を維持できる
- `Replace()` が selector を省略できる
- `Omit()` が末尾 segment を省略できる
- 全segmentの `Omit()` を拒否する

### 13.3 Context

- root直下では ancestor が空になる
- nested node では ancestor が root 順に渡る
- scalar/object member で `Node` が渡る
- container element で sibling 一覧が渡る
- container presence entry で `Node == null`、siblings が空になる

### 13.4 Escape

- key text 内の `]` を escape する
- key text 内の `\` を escape する
- key text 先頭の `#` を escape する
- projector が返した member 名の許容範囲を検証する
- 不正な member 名から不正 path を生成しない

### 13.5 Pattern matching

- projection extension が `ProjectedPath` を照合する
- projected ancestor patternがprojected descendant pathへ一致する
- segment境界の異なるprojected sibling pathへ一致しない
- 既存 entry extension が標準 `Path` を照合し続ける
- `[*]` が projected key selector に一致する
- `[*]` が projected ordinal selector に一致する
- exact key と exact ordinal を区別する

### 13.6 再帰 model E2E

- runtimeの `Name` から再帰 path を投影できる
- model slot 間の名前が一致しないsegmentだけ標準名へfallbackする
- keyed container の selector を維持して名前変更できる
- 利用側定義 path が `GetNodeByPath()` の住所にならないことを確認する
- 標準 path は引き続き node を解決できる

### 13.7 重複と非影響

- 同じ `ProjectedPath` を持つ複数entryを保持する
- projection呼び出し前後で標準entryが変化しない
- `CompareResult<T>.Root` が変化しない
- `CompareResult<T>.Issues` が変化しない
- `CompareResult<T>.HasError` が変化しない
- `Root.HasDifferences()` が変化しない
- equal modelではprojection entryを返さない

### 13.8 検証単位

focused testとして次を追加する。

```text
Unit: 20 tests
E2E:   6 tests
Total: 26 tests
```

Issue #48の指摘対応では、利用側定義pathに対する祖先一致、segment境界、および標準path判定の非影響を専用unit testで追加する。

加えてrepository全体の既存testを実行し、標準 path、path access、path pattern、比較結果の回帰がないことを確認する。

## 14. 実装ファイル

```text
src/SSC/
  ParallelDiffPathProjection.cs
  ParallelDiffPathSegments.cs
  ParallelPathAccessExtensions.cs

src/SSC/Internal/
  ParallelDiffPathFormatter.cs

tests/SSC.Unit.Tests/
  ParallelDiffPathProjectionUnitTests.cs
  ParallelDiffPathProjectionAncestorUnitTests.cs

tests/SSC.E2E.Tests/
  ParallelDiffPathProjectionE2ETests.cs

doc/design/detail/
  11-DiffEntryCustomPath.md
```

### 14.1 `ParallelDiffPathSegments.cs`

公開segmentとselectorの構造、factory、validationを担当する。

### 14.2 `ParallelDiffPathProjection.cs`

投影器、文脈情報、segment投影結果、entry投影結果、pattern matching extensionを担当する。

### 14.3 `ParallelPathAccessExtensions.cs`

既存差分entryと利用側定義pathを同じtree走査から生成する。

### 14.4 `ParallelDiffPathFormatter.cs`

標準pathと利用側定義pathの文字列化、selector表現、key escapeを担当する。

## 15. 完了条件

- 既存 `ParallelDiffEntry.Path` が標準 path として維持される
- 利用側が node context から segment を置換または省略できる
- 利用側が path 文字列を手作業で組み立てる必要がない
- 再帰 model でも runtime の意味名を使った利用側定義 path を生成できる
- selector の種類と既存表現を維持できる
- 利用側定義 path を既存 `ParallelDiffPathPattern` で絞り込める
- 利用側定義 path の祖先patternで子孫差分をまとめて絞り込める
- segment境界の異なる利用側定義 path に誤一致しない
- XML 等の domain 固有規則が SSC production code に入らない
- `CompareConfiguration`、metadata resolution、比較 tree 構築へ影響しない
- 利用側定義 path の重複を許容し、標準 path でentryを識別できる
- focused unit/E2E testが通る
- repository全体の既存testが通る
