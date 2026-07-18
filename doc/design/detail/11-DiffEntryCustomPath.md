# 差分 entry の利用側定義 path 投影（Diff Entry Custom Path Projection）

## 1. 目的

`CompareResult<T>.GetDiffEntries()` が返す差分 entry に対し、SSC が生成する既存 path を変更せず、利用側が用途に応じた別の path 表現を生成できるようにする。

再帰的な model では、既存 path が次のように同じ member 名を繰り返すことがある。

```text
Root.Children[#0].Children[#0].Fields[#0].Value
```

利用側が各 node の `Name` など、比較対象 instance が持つ実際の値を使い、次のような読みやすい path を生成できることを目的とする。

```text
Root.Child1[#0].Child2[#0].Attribute1[#0].Value
```

本機能は path の表示、分類、絞り込みを改善するための比較後処理である。比較対象、比較結果、container 要素の対応付けには影響しない。

## 2. 用語

本設計では英語の用語だけで意味を省略せず、次の意味で使用する。

| 用語 | 本設計での意味 |
|---|---|
| entry | 差分1件を表す `ParallelDiffEntry` またはその関連結果 |
| node | SSC の比較 tree を構成する `IParallelNode` 1件 |
| tree | 親子関係で構成される階層構造 |
| member | C# model の property または field |
| container | `List`、array、`IEnumerable`、dictionary など複数要素を保持する member |
| sequence | 順序を持つ container 要素列 |
| key | container 要素を対応付ける識別値 |
| ordinal | key を持たない sequence 内の並び順。`0` から始まる |
| model slot | 比較に渡した各 model の値位置。`modelIndex` と同じ意味 |
| standard path / canonical path | SSC が `ParallelDiffEntry.Path` に格納する既存の正式 path。本設計では「標準 path」と呼ぶ |
| custom path | 利用側が標準 path から生成する別表現。本設計では「利用側定義 path」と呼ぶ |
| alias | 同じ対象に付ける別名 |
| projection | 元の情報を保持したまま、別の見え方へ変換する処理。本設計では「投影」と呼ぶ |
| projector | 投影規則を実装する利用側 component。本設計では「投影器」と呼ぶ |
| segment | dot で区切られた path の構成要素1件 |
| selector | container 要素を識別する `[]` 部分 |
| context | 投影器の判断材料として SSC が渡す現在位置の情報。本設計では「文脈情報」と呼ぶ |
| sibling | 同じ親を持つ兄弟 node |
| ancestor | 現在位置より上位にある祖先 node |
| fallback | 利用側定義名を決められない場合に安全な既定動作へ戻すこと |
| pattern | path を照合するためのひな形 |
| wildcard | 任意の値へ一致する記号。既存 API では selector の `[*]` |

### 2.1 標準 path

標準 path は、SSC が現在 `ParallelDiffEntry.Path` に格納している正式な path である。

`canonical` は「基準となる正式な表現」という意味だが、本設計では英語だけで呼ばず、以降は「標準 path」と記載する。

例:

```text
Groups[1].Items[100].MetricA
Root.Children[#0].Fields[#0].Value
```

標準 path は SSC の比較 tree 上の位置を表す。

- `GetNodeByPath()` で node を取得できる
- `ParallelDiffEntry.Path` と `ParallelDiffEntry.ParentPath` に使用される
- 既存 `ParallelDiffEntry.PathMatches()` の照合対象になる
- 後方互換の対象である

### 2.2 利用側定義 path

利用側定義 path は、利用側が標準 path を別の意味表現へ変換した path である。

例:

```text
標準 path:
Root.Children[#0].Fields[#0].Value

利用側定義 path:
Root.Child1[#0].Attribute1[#0].Value
```

利用側定義 path は表示、分類、絞り込みに使用する。初期実装では node の住所として扱わない。

### 2.3 Segment と selector

次の path は3 segment で構成される。

```text
Root.Children[#0].Value
```

```text
Root
Children[#0]
Value
```

`Children[#0]` の `[#0]` が selector である。

```text
Items[A]   key selector。key text A で要素を識別する
Items[#0]  ordinal selector。並び順 0 で要素を識別する
```

具体的な path segment では wildcard `[*]` を使用しない。`[*]` は `ParallelDiffPathPattern` 側だけの表現とする。

### 2.4 投影と投影器

投影は、標準 path の各 segment を次のいずれかへ変換する処理である。

- 標準 segment のまま維持する
- 別の具体的 segment へ置き換える
- 利用側定義 path から省略する

投影器は、この判断を行う利用側実装である。

SSC は node と path の構造情報を投影器へ渡す。投影器は利用側定義 path での segment を返す。

SSC は `Name`、`Children`、`Fields` などの業務上の意味を解釈しない。

### 2.5 Fallback

利用側が runtime の値から別名を決定できない場合は、標準 segment の維持を推奨する。

```text
Children[#0]
    ↓ 別名を決定できない
Children[#0]
```

SSC は model slot 間のどの値を採用すべきか自動判断しない。

## 3. 現状と問題

現行 `GetDiffEntries()` は、比較 tree を再帰的に走査し、member 名と selector を連結して `ParallelDiffEntry.Path` を生成する。

container child では次の規則を使用する。

- `IParallelNode.KeyText` がある場合は key selector
- `IParallelNode.KeyText` がない場合は ordinal selector

この規則は SSC の構造を正確に表すが、汎用的な再帰 model では利用者が知りたい意味を直接表さない場合がある。

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

実データが次の場合、利用者には `Name` の方が意味を持つ。

```text
Root
└─ Child1
   └─ Child2
      └─ Attribute1 = "0"
```

標準 path:

```text
Root.Children[#0].Children[#0].Fields[#0].Value
```

利用側が望む path:

```text
Root.Child1[#0].Child2[#0].Attribute1[#0].Value
```

SSC が `Name` を自動採用することはできない。

- `Name` が path 名であるとは限らない
- 別 model では `Code`、`Id`、`Label` が意味名かもしれない
- model slot 間で名前が異なる場合の採用規則は利用側ごとに異なる
- 同名 sibling の区別規則も利用側ごとに異なる

SSC は判断材料だけを渡し、意味の決定は利用側に委ねる必要がある。

## 4. 責務境界

### 4.1 SSC の責務

SSC は次を担当する。

1. 既存の標準 path を変更せず生成する
2. 標準 path を構造化された segment と selector として扱う
3. path 生成時に把握している node の文脈情報を投影器へ渡す
4. 投影器が返した具体的 segment を標準 path と同じ grammar で文字列化する
5. 標準 entry と利用側定義 path を同じ結果から参照できるようにする
6. 利用側定義 path に既存 `ParallelDiffPathPattern` を適用できるようにする
7. 既存 `GetDiffEntries()` の件数、順序、path、node 参照を維持する

### 4.2 利用側の責務

利用側は次を担当する。

1. どの member を置換または省略するか決める
2. node のどの runtime 値を利用側定義名として使うか決める
3. model slot 間で候補名が異なる場合の規則を決める
4. 同名 sibling をどう区別するか決める
5. 名前を決定できない場合の fallback を決める
6. 利用側定義 path をレポート、ログ、分類へどう表示するか決める

### 4.3 SSC が解釈しない情報

SSC は次のような model 固有の意味を自動判定しない。

```text
Name      = node 名
Children  = XML の子要素
Fields    = 属性
Value     = レポートでは省略可能
```

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
- `[#0]` を表示上の `[0]` へ自動変換する処理
- 正規表現による path 変換

`CompareConfiguration` に投影器を置かない理由は、同じ比較結果へ複数の見え方を適用できるようにするためである。

```csharp
var reportPaths = result.GetDiffEntryPathProjections(reportProjector);
var logPaths = result.GetDiffEntryPathProjections(logProjector);
```

投影は比較設定ではなく、比較後の出力表現である。

## 6. 固定 alias attribute を対象外とする理由

### 6.1 Alias attribute とは

C# の attribute は、class、property、field などに付加する metadata である。

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

再帰 model で必要になる名前は、property に固定された名前ではなく各 node の runtime 値である。

```csharp
public sealed class TreeNode
{
    public string Name { get; init; } = string.Empty;

    public List<TreeNode> Children { get; init; } = [];
}
```

同じ `Children` property の要素が、実行時にはそれぞれ異なる名前を持つ。

```text
Child1
Child2
Child3
```

次の固定 attribute では表現できない。

```csharp
[DiffPathAlias("Child1")]
public List<TreeNode> Children { get; init; }
```

この指定ではすべての child が `Child1` になる。

### 6.3 初期設計での判断

固定 alias attribute は初期対象に含めない。

- runtime の node 値を参照できない
- `TypeMetadataResolver` に出力表現の責務が入る
- 比較 metadata と比較後の表示規則が混ざる
- 同じ比較結果へ異なる path 表現を適用しにくくなる

固定 member rename の需要が別途明確になった場合は、本設計の投影器を簡単に構築する補助 API として後続設計する。

## 7. 公開 API

### 7.1 利用側定義 path の取得

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

### 7.2 投影器（projector）

```csharp
public interface IParallelDiffPathProjector
{
    ParallelDiffPathSegmentProjection Project(
        ParallelDiffPathProjectionContext context);
}
```

`Project()` は標準 path の segment 1件に対し、利用側定義 path での扱いを返す。

投影器は同じ入力に対して同じ結果を返す決定的な実装とする。

「決定的」は、同じ入力なら毎回同じ結果になることを意味する。時刻、乱数、外部の可変状態へ依存させない。

### 7.3 投影時の文脈情報（projection context）

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

| Property | 内容 |
|---|---|
| `StandardSegment` | SSC が生成する標準 path segment |
| `ParentNode` | `StandardSegment` を所有する親 node |
| `Node` | segment が指す現在の node。container presence entry では `null` |
| `Siblings` | 同じ `ParallelChildSet` に属する node 一覧。container presence entry では空 |

`container presence entry` は、空 container と missing container のように、要素 node を作れない container 自体の存在差分を表す entry である。

### 7.4 具体的な path segment

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

`WithMemberName()` は selector を維持して member 名だけを変更する。

```csharp
ParallelDiffPathSegment standard =
    ParallelDiffPathSegment.Ordinal("Children", 0);

ParallelDiffPathSegment custom =
    standard.WithMemberName("Child1");
```

```text
Children[#0]
    ↓
Child1[#0]
```

### 7.5 選択子（selector）

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

path segment の生成 method が selector の整合性を保証する。

- `Key()` は空でない `KeyText` を持つ
- `Ordinal()` は非負の `Ordinal` を持つ
- 具体 path なので wildcard は持たない

### 7.6 Segment 投影結果

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
Children[#0]
    ↓
Child1[#0]
```

#### `Omit`

利用側定義 path から segment を省略する。

```text
DocumentWrapper.Root.Children[#0]
    ↓ DocumentWrapper を省略
Root.Children[#0]
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

| Property | 内容 |
|---|---|
| `Entry` | 標準差分 entry |
| `Entry.Path` | SSC が生成した標準 path |
| `ProjectedPath` | 投影器を適用した利用側定義 path |
| `ProjectedParentPath` | 標準 parent path と同じ segment 範囲へ投影器を適用した path |

```csharp
ParallelDiffEntryPathProjection projected = projections[0];

string standardPath = projected.Entry.Path;
string customPath = projected.ProjectedPath;
```

```text
standardPath:
Root.Children[#0].Fields[#0].Value

customPath:
Root.Child1[#0].Attribute1[#0].Value
```

別々に呼び出した `GetDiffEntries()` と `GetDiffEntryPathProjections()` の entry について、object の参照一致は保証しない。件数、順序、property 値、node 参照の意味を一致させる。

### 7.8 Pattern 照合

```csharp
public static class ParallelDiffEntryPathProjectionExtensions
{
    public static bool PathMatches(
        this ParallelDiffEntryPathProjection projection,
        ParallelDiffPathPattern pattern);
}
```

この同名別引数版は `ProjectedPath` を照合する。

既存 API は変更しない。

```csharp
entry.PathMatches(pattern);
```

既存 API は引き続き `ParallelDiffEntry.Path`、つまり標準 path を照合する。

```csharp
ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
    "Root.Child1[*].Child2[*].Attribute1[*].Value");

ParallelDiffEntryPathProjection[] matched = result
    .GetDiffEntryPathProjections(projector)
    .Where(entry => entry.PathMatches(pattern))
    .ToArray();
```

## 8. 投影規則

### 8.1 標準 entry の維持

`GetDiffEntryPathProjections()` が返す `Entry` は、既存 `GetDiffEntries()` と同じ標準情報を持つ。

次を変更しない。

- entry 件数
- entry 順序
- `Entry.Path`
- `Entry.ParentPath`
- `Entry.Kind`
- `Entry.ParentNode`
- `Entry.Node`
- `Entry.Values`

### 8.2 Segment 単位の処理

標準 path を root 側から segment 単位で処理する。

```text
Root
Children[#0]
Fields[#0]
Value
```

各 segment に投影器を適用し、結果を順番に連結する。

### 8.3 Member 名の検証と key text の escape

現行 path grammar は member 名の escape を定義していない。

そのため、利用側が返す `MemberName` は次を満たす必要がある。

- 空でない
- `.` を含まない
- `[` を含まない
- `]` を含まない

不正な member 名は `ParallelDiffPathSegment` の生成時に `ArgumentException` とする。

例:

```text
Valid:   Child1
Invalid: Child.1
Invalid: Child[1]
```

key text は既存規則に従って SSC が escape する。

- `]` を `\]` として表す
- `\` を `\\` として表す
- 先頭 `#` を `\#` として表す

利用側は path 文字列を手作業で連結しない。

### 8.4 Selector の維持

member 名だけを変える場合は `WithMemberName()` を使用し、key と ordinal の意味を維持する。

```text
Children[#0]
    ↓
Child1[#0]
```

次の自動変換は行わない。

```text
[#0] -> [0]
```

`[#0]` は ordinal、`[0]` は key text `0` を表すため、意味が異なる。

レポート上だけ `[0]` と表示したい場合は、SSC の path grammar ではなく利用側の表示処理で変換する。

### 8.5 Omit

中間 segment と末尾 segment の両方を省略可能とする。

```text
DocumentWrapper.Root.Child1[#0].Value
    ↓ DocumentWrapper と Value を省略
Root.Child1[#0]
```

末尾 segment を省略した場合、`ProjectedPath` と `ProjectedParentPath` が同じ文字列になることがある。

利用側定義 path は node lookup 用の住所ではないため、これを許容する。

### 8.6 空 path の拒否

すべての segment が `Omit` され、`ProjectedPath` が空になる場合は `InvalidOperationException` とする。

空文字を有効な利用側定義 path として返さない。

### 8.7 重複 path

複数 entry が同じ `ProjectedPath` になることを許容する。

```text
Root.Item.Value
Root.Item.Value
```

SSC は利用側定義 path の一意性を保証しない。

- 投影は表示と絞り込みのための機能である
- 各 entry は `Entry.Path` により標準位置を保持する
- 同名 node の区別規則は利用側固有である

pattern は一致したすべての entry を返す。

### 8.8 Model slot 間の値

SSC は複数 model slot のどの値を採用するか決めない。

投影器は `IParallelNode.Count`、`GetValue()`、`GetState()` を使って判断する。

利用側規則の例:

1. `Missing` ではない slot から候補名を取得する
2. すべて同じ場合だけその名前を使う
3. 異なる場合は `KeepStandard()` へ fallback する

この規則自体は SSC の製品コードへ実装しない。

### 8.9 Container presence entry

空 container と missing container の存在差分では、対応する child node が存在しない。

その場合の文脈情報は次とする。

```text
Current.Node       = null
Current.Siblings   = empty
Current.ParentNode = container owner node
```

投影器は node 値を必要とする置換を行えないため、通常は `KeepStandard()` を返す。

### 8.10 投影器の呼び出し

投影器は同期的に呼び出す。初期実装では並列呼び出しを行わない。

同じ node に対応する segment が複数 entry の path に現れる場合、投影器が複数回呼ばれる可能性がある。投影器は呼び出し回数へ依存しない実装とする。

## 9. 利用例

### 9.1 利用側投影器の概念例

次は利用方法を示す概念例であり、SSC が特定 model 向けに提供する投影器ではない。

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

### 9.2 絞り込み

```csharp
IReadOnlyList<ParallelDiffEntryPathProjection> projections =
    result.GetDiffEntryPathProjections(new NamedTreePathProjector());

ParallelDiffPathPattern customPattern = ParallelDiffPathPattern.Parse(
    "Root.Child1[*].Child2[*].Attribute1[*].Value");

ParallelDiffEntryPathProjection[] customMatches = projections
    .Where(projection => projection.PathMatches(customPattern))
    .ToArray();
```

標準 path に対する既存絞り込みも同時に利用できる。

```csharp
ParallelDiffPathPattern standardPattern = ParallelDiffPathPattern.Parse(
    "Root.Children[*].Children[*].Fields[*].Value");

ParallelDiffEntry[] standardMatches = result
    .GetDiffEntries()
    .Where(entry => entry.PathMatches(standardPattern))
    .ToArray();
```

## 10. 例外と失敗

- `GetDiffEntryPathProjections(null result, ...)` は `ArgumentNullException`
- `GetDiffEntryPathProjections(..., null projector)` は `ArgumentNullException`
- `ParallelDiffPathSegment.Member(null または空文字)` は `ArgumentException`
- member 名に `.`, `[`, `]` が含まれる場合は `ArgumentException`
- `ParallelDiffPathSegment.Key(..., null または空文字)` は `ArgumentException`
- `ParallelDiffPathSegment.Ordinal(..., 負数)` は `ArgumentOutOfRangeException`
- `Replace(null)` は `ArgumentNullException`
- 全 segment を省略した場合は `InvalidOperationException`
- 投影器が送出した例外は握りつぶさず、そのまま呼び出し元へ伝播する
- 投影器が不正な segment を返すことは生成 method の入力検証で防ぐ

## 11. 実装方針

### 11.1 標準 path の segment 化

現行 `AddNodeDiffEntries()` と `AddChildSetDiffEntries()` が文字列 path を直接連結する処理を、内部の segment stack を使う形へ整理する。

`stack` は、現在の再帰位置までの要素を順番に積み上げた構造である。

```text
Root
Root + Children[#0]
Root + Children[#0] + Fields[#0]
Root + Children[#0] + Fields[#0] + Value
```

既存 `Path` の文字列結果は完全一致させる。

### 11.2 Node context stack

path segment と同じ階層で次を保持する。

```text
StandardSegment
ParentNode
Node
Siblings
```

差分 entry を生成する時点で、この stack を root から順に投影器へ渡す。

標準 path 文字列を再解析しない。`GetNodeByPath()` で root から node を再探索しない。

### 11.3 文字列化処理の共有

標準 path と利用側定義 path は同じ文字列化処理を使用する。

次を共有する。

- dot 連結
- `[]` の生成
- key text の escape
- 先頭 `#` の escape
- ordinal の `#` 表記

標準 path と利用側定義 path で異なる grammar を作らない。

### 11.4 `ParallelDiffPathPattern` の再利用

利用側定義 path は既存 path grammar で文字列化するため、既存 pattern parser と照合処理を再利用する。

新しい wildcard grammar は追加しない。

### 11.5 比較処理との分離

本機能では次を変更しない。

- `ParallelCompareApi.Compare()`
- `CompareConfiguration`
- `CompareIgnoreAttribute`
- `CompareKeyAttribute`
- `TypeMetadataResolver`
- container normalization
- key union
- node の value/state 判定

本機能は比較結果の列挙時にだけ動作する。

```text
models
  -> metadata resolution
  -> comparison tree construction
  -> CompareResult<T>
  -> GetDiffEntries()                       標準 entry
  -> GetDiffEntryPathProjections(projector) 利用側定義 path 付き結果
```

`metadata resolution` は、比較対象 member や `CompareKey` などの metadata を解決する既存処理である。

## 12. 後方互換

### 12.1 既存 API

次の公開形式と挙動を変更しない。

```csharp
result.GetDiffEntries();
entry.PathMatches(pattern);
result.GetNodeByPath(path);
result.GetValueByPath(path, modelIndex);
result.GetStateByPath(path, modelIndex);
```

### 12.2 `ParallelDiffEntry.ToString()`

既存どおり `ParallelDiffEntry.Path`、つまり標準 path を表示する。

利用側定義 path を暗黙に使用しない。

### 12.3 標準 path の解決可能性

次の既存契約を維持する。

```csharp
entry.Node == result.GetNodeByPath(entry.Path)
```

利用側定義 path にはこの契約を適用しない。

## 13. テスト方針

### 13.1 既存回帰

- 既存 `GetDiffEntries()` が entry 件数を維持する
- 標準 `Path` と `ParentPath` が完全一致する
- entry 順序が変わらない
- `Entry.Node` が `GetNodeByPath(Entry.Path)` で解決できる
- `ParallelDiffEntry.ToString()` が変わらない
- 既存 `ParallelDiffPathPattern` test を変更せず通す
- 既存 `CompareIgnore` test を変更せず通す

### 13.2 Segment 投影

- `KeepStandard()` が標準 segment を維持する
- `Replace()` が member 名を変更できる
- `Replace()` が key selector を維持できる
- `Replace()` が ordinal selector を維持できる
- `Omit()` が中間 segment を省略できる
- `Omit()` が末尾 segment を省略できる
- 全 segment の `Omit()` を拒否する

### 13.3 文脈情報

- root 直下では ancestor が空になる
- nested node では ancestor が root 順に渡る
- scalar/object member で `Node` が渡る
- container element で sibling 一覧が渡る
- ordinal child で標準 selector が `[#n]` になる
- key child で標準 selector が `[key]` になる
- container presence entry で `Node == null`、siblings が空になる

### 13.4 Grammar と escape

- member 名の `.`, `[`, `]` を拒否する
- key text 内の `]` を escape する
- key text 内の `\` を escape する
- key text 先頭の `#` を escape する
- 不正な segment から不正 path を生成しない

### 13.5 Pattern 照合

- projection extension が `ProjectedPath` を照合する
- 既存 entry extension が標準 `Path` を照合し続ける
- `[*]` が projected key selector に一致する
- `[*]` が projected ordinal selector に一致する
- exact key と exact ordinal を区別する

### 13.6 重複と fallback

- 同じ `ProjectedPath` を持つ複数 entry を保持する
- `KeepStandard()` を返した segment だけ標準名へ戻る
- 一部 segment だけ置換できる
- 投影器の例外を呼び出し元へ伝播する

### 13.7 比較結果への非影響

- `CompareResult<T>.Root` が変化しない
- `CompareResult<T>.Issues` が変化しない
- `CompareResult<T>.HasError` が変化しない
- `Root.HasDifferences()` が変化しない
- projection 呼び出し前後で既存 `GetDiffEntries()` の結果が変化しない

## 14. 実装対象の想定

初期実装では概ね次を対象とする。

```text
src/SSC/
  ParallelDiffPathProjection.cs
  ParallelDiffPathSegments.cs
  ParallelPathAccessExtensions.cs

src/SSC/Internal/
  path segment formatter
  traversal context helper

tests/SSC.Unit.Tests/
  ParallelDiffPathProjectionUnitTests.cs

tests/SSC.E2E.Tests/
  ParallelDiffPathProjectionE2ETests.cs

doc/design/detail/
  02-PublicApi.md
  11-DiffEntryCustomPath.md
```

実際の class 分割は既存 source 配置と責務に合わせて調整する。

## 15. 完了条件

- 既存 `ParallelDiffEntry.Path` が標準 path として維持される
- 利用側が node の文脈情報から segment を置換または省略できる
- 利用側が path 文字列を手作業で組み立てる必要がない
- 再帰 model でも意味名を使った利用側定義 path を生成できる
- 利用側定義 path を既存 `ParallelDiffPathPattern` で絞り込める
- XML 等の domain 固有規則が SSC の製品コードに入らない
- `CompareConfiguration`、metadata resolution、比較 tree 構築へ影響しない
- 既存 path access、diff entry、filter の全回帰が通る
