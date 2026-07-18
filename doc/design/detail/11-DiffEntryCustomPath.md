# 差分 entry の利用側定義 path 投影

## 1. 目的

`CompareResult<T>.GetDiffEntries()` が返す差分 entry に対し、SSC が生成する既存 path を変更せず、利用側が用途に応じた別の path 表現を生成できるようにする。

再帰的な model では、標準 path が次のように同じ member 名を繰り返すことがある。

```text
Root.Children[0].Children[0].Fields[0].Value
```

この path は SSC の model 構造として正しいが、利用者が知りたい node 名や field 名を直接表していない。

利用側が各 node の `Name` など、比較対象 instance が持つ実行時の値を使い、次のような path を生成できるようにする。

```text
Root.Child1[0].Child2[0].Attribute1[0].Value
```

本機能は path の表示、分類、絞り込みを改善するための比較後処理である。

次には影響しない。

- 比較対象 member
- model 間の比較結果
- container 要素の対応付け
- `CompareKey`
- `CompareResult<T>.Root`
- `CompareResult<T>.Issues`

## 2. 用語

本設計では英語の用語だけで意味を省略せず、次の意味で使用する。

| 用語 | 本設計での意味 |
|---|---|
| entry | 差分1件を表す `ParallelDiffEntry`、またはその関連結果 |
| node | SSC の比較 tree を構成する `IParallelNode` 1件 |
| tree | 親子関係で構成される階層構造 |
| member | C# model の property または field |
| container | `List`、array、`IEnumerable`、dictionary など複数要素を保持する member |
| sequence | 順序を持つ container 要素列 |
| key | container 要素を対応付ける識別値 |
| index alignment | `CompareKey` がない sequence を同じ index 同士で対応付ける処理。本設計では「index 対応付け」と呼ぶ |
| ordinal | node の `KeyText` がない場合に、同じ child set 内の並び順で識別する値。`0` から始まる |
| model slot | 比較へ渡した各 model の値位置。`modelIndex` と同じ意味 |
| standard path / canonical path | SSC が `ParallelDiffEntry.Path` に格納する既存の正式 path。本設計では「標準 path」と呼ぶ |
| custom path | 利用側が標準 path から生成する別表現。本設計では「利用側定義 path」と呼ぶ |
| alias | 同じ対象へ付ける別名 |
| projection | 元の情報を保持したまま別の見え方へ変換する処理。本設計では「投影」と呼ぶ |
| projector | 投影規則を実装する利用側 component。本設計では「投影器」と呼ぶ |
| segment | dot で区切られた path の構成要素1件 |
| selector | container 要素を識別する `[]` 部分 |
| context | 投影器の判断材料として SSC が渡す現在位置の情報。本設計では「文脈情報」と呼ぶ |
| sibling | 同じ親を持つ兄弟 node |
| ancestor | 現在位置より上位にある祖先 node |
| fallback | 利用側定義名を決められない場合に安全な既定動作へ戻すこと |
| pattern | path を照合するためのひな形 |
| wildcard | 任意の selector へ一致する記号。既存 API では `[*]` |
| formatter | 構造化された segment を path 文字列へ変換する処理 |
| pipeline | 入力 model から比較結果を構築する一連の処理 |
| deterministic | 同じ入力なら毎回同じ結果になる性質。本設計では「決定的」と呼ぶ |
| validation | API へ渡された値が契約を満たすか検査する処理 |

### 2.1 標準 path（canonical path）

`canonical` は「基準となる正式な表現」という意味である。

本設計では英語だけで呼ばず、「標準 path」と記載する。

標準 path は、SSC が現在 `ParallelDiffEntry.Path` に格納している比較 tree 上の正式な住所である。

例:

```text
Groups[1].Items[100].MetricA
Root.Children[0].Fields[0].Value
```

標準 path は次の既存契約を持つ。

- `GetNodeByPath()` で node を取得できる
- `ParallelDiffEntry.Path` と `ParallelDiffEntry.ParentPath` に使用される
- 既存 `ParallelDiffEntry.PathMatches()` の照合対象になる
- `ParallelDiffEntry.ToString()` に使用される
- 後方互換の対象である

### 2.2 利用側定義 path（custom path）

`custom` は「利用側が用途に合わせて定義する」という意味である。

利用側定義 path は、標準 path を別の意味表現へ変換した path である。

```text
標準 path:
Root.Children[0].Fields[0].Value

利用側定義 path:
Root.Child1[0].Attribute1[0].Value
```

利用側定義 path は次に使用する。

- report 表示
- log 表示
- 差分分類
- `ParallelDiffPathPattern` による絞り込み

初期実装では node の住所として扱わない。

### 2.3 Segment

`segment` は、dot で区切られた path の構成要素1件を指す。

```text
Root.Children[0].Value
```

この path は次の3 segment で構成される。

```text
Root
Children[0]
Value
```

### 2.4 Selector

`selector` は、container member 配下の要素を識別する `[]` 部分を指す。

```text
Items[A]
Items[0]
Items[#0]
```

selector は2種類ある。

| 表現 | 種類 | 意味 |
|---|---|---|
| `[A]` | key selector | `KeyText == "A"` の node |
| `[0]` | key selector | `KeyText == "0"` の node |
| `[#0]` | ordinal selector | `KeyText` がない child set の0番目の node |

`[0]` と `[#0]` は意味が異なる。

現在の index 対応付けでは、SSC が index を `KeyText` として保持するため、標準 path は通常 `[0]` になる。

```text
Children[0]
```

一方、`IParallelNode.KeyText == null` の child は ordinal selector を使う。

```text
Children[#0]
```

具体的な path segment では wildcard `[*]` を使用しない。`[*]` は `ParallelDiffPathPattern` 側だけの表現とする。

### 2.5 投影（projection）

投影は、標準 path の各 segment を次のいずれかへ変換する処理である。

- 標準 segment のまま維持する
- 別の具体的 segment へ置き換える
- 利用側定義 path から省略する

標準 path 自体は変更しない。

### 2.6 投影器（projector）

投影器は、各 segment の投影規則を実装する利用側 component である。

SSC は node と path の構造情報を投影器へ渡す。

投影器は、利用側定義 path でその segment をどう扱うか返す。

SSC は次の意味を解釈しない。

```text
Name      = node 名
Children  = XML の子要素
Fields    = 属性
Value     = report では省略可能
```

### 2.7 Fallback

`fallback` は、利用側が別名を決定できない場合に安全な既定動作へ戻すことを意味する。

推奨 fallback は標準 segment の維持である。

```text
Children[0]
    ↓ 名前を決定できない
Children[0]
```

SSC は model slot 間のどの値を採用すべきか自動判断しない。

## 3. 現状と問題

現行 `GetDiffEntries()` は比較 tree を再帰的に走査し、member 名と selector を連結して `ParallelDiffEntry.Path` を生成する。

container child では次の規則を使う。

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

実データ:

```text
Root
└─ Child1
   └─ Child2
      └─ Attribute1 = "0"
```

標準 path:

```text
Root.Children[0].Children[0].Fields[0].Value
```

利用側が望む path:

```text
Root.Child1[0].Child2[0].Attribute1[0].Value
```

SSC が `Name` を自動採用することはできない。

- `Name` が path 名であるとは限らない
- 別 model では `Code`、`Id`、`Label` が意味名かもしれない
- model slot 間で名前が異なる場合の採用規則は利用側ごとに異なる
- 同名 sibling の区別規則も利用側ごとに異なる

SSC は判断材料だけを渡し、意味の決定は利用側へ委ねる必要がある。

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
8. key text の escape と member 名の validation を一元管理する

### 4.2 利用側の責務

利用側は次を担当する。

1. どの member を置換または省略するか決める
2. node のどの実行時値を利用側定義名として使うか決める
3. model slot 間で候補名が異なる場合の規則を決める
4. 同名 sibling をどう区別するか決める
5. 名前を決定できない場合の fallback を決める
6. 利用側定義 path を report、log、分類へどう表示するか決める

### 4.3 SSC が解釈しない情報

SSC は次のような domain 固有情報を自動判定しない。

```text
TreeNode.Name       が element 名である
NamedValue.Name     が attribute 名である
Children            が XML の子要素である
Fields              が XML の属性である
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
- report 固有の `[0]` / `[#0]` 表記変換
- 非同期 projector

## 6. Alias attribute

### 6.1 どのようなものか

`alias attribute` は、C# member に固定的な別名を指定する attribute を意味する。

仮に次の API があるとする。

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

この場合は固定的な member rename を表現できる。

```text
Items[100].Price
    ↓
Lines[100].Price
```

### 6.2 今回の要求を満たせない理由

再帰 model で必要になる名前は property に固定された名前ではなく、各 node の実行時値である。

```csharp
public sealed class TreeNode
{
    public string Name { get; init; } = string.Empty;

    public List<TreeNode> Children { get; init; } = [];
}
```

同じ `Children` property の要素が実行時にはそれぞれ異なる名前を持つ。

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

### 6.3 初期実装での判断

固定 alias attribute は初期対象に含めない。

- 実行時の node 値を参照できない
- `TypeMetadataResolver` に出力表現の責務が入る
- 比較 metadata と比較後の表示規則が混ざる
- 同じ比較結果へ異なる path 表現を適用しにくくなる

固定 member rename の需要が明確になった場合は、本設計の投影器を簡単に構築する補助 API として別途設計する。

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

### 7.2 投影器

```csharp
public interface IParallelDiffPathProjector
{
    ParallelDiffPathSegmentProjection Project(
        ParallelDiffPathProjectionContext context);
}
```

`Project()` は標準 path の segment 1件に対し、利用側定義 path での扱いを返す。

投影器は同じ入力に対して同じ結果を返す決定的な実装とする。

時刻、乱数、外部の可変状態へ依存させない。

### 7.3 投影時の文脈情報

```csharp
public sealed class ParallelDiffPathProjectionContext
{
    public IReadOnlyList<ParallelDiffPathNodeContext> Ancestors { get; }

    public ParallelDiffPathNodeContext Current { get; }
}
```

`Ancestors` は root 側から現在の親までの順序で保持する。

現在位置自身は含めず、`Current` で参照する。

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

index 対応付けの node:

```csharp
ParallelDiffPathSegment standard =
    ParallelDiffPathSegment.Key("Children", "0");

ParallelDiffPathSegment custom =
    standard.WithMemberName("Child1");
```

```text
Children[0]
    ↓
Child1[0]
```

ordinal node:

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
Children[0]
    ↓
Child1[0]
```

#### `Omit`

利用側定義 path から segment を省略する。

```text
DocumentWrapper.Root.Children[0]
    ↓ DocumentWrapper を省略
Root.Children[0]
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
Root.Children[0].Fields[0].Value

customPath:
Root.Child1[0].Attribute1[0].Value
```

別々に呼び出した `GetDiffEntries()` と `GetDiffEntryPathProjections()` の entry について、object の参照一致は保証しない。

次は一致させる。

- 件数
- 順序
- property 値
- `Node` と `ParentNode` の意味

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
Children[0]
Fields[0]
Value
```

各 segment に投影器を適用し、結果を順番に連結する。

### 8.3 Member 名の validation

現行 path grammar は member 名の escape を定義していない。

そのため、利用側が返す `MemberName` は次を満たす必要がある。

- 空でない
- `.` を含まない
- `[` を含まない
- `]` を含まない

不正な member 名は `ParallelDiffPathSegment` の生成時に `ArgumentException` とする。

```text
Valid:   Child1
Invalid: Child.1
Invalid: Child[1]
```

### 8.4 Key text の escape

key text は既存規則に従って SSC が escape する。

- `]` を `\]` として表す
- `\` を `\\` として表す
- 先頭 `#` を `\#` として表す

利用側は path 文字列を手作業で連結しない。

### 8.5 Selector の維持

member 名だけを変える場合は `WithMemberName()` を使用し、key と ordinal の意味を維持する。

```text
Children[0]
    ↓
Child1[0]
```

次の自動変換は行わない。

```text
[0] <-> [#0]
```

`[0]` は key text `0`、`[#0]` は ordinal `0` を表すため、意味が異なる。

report 上だけ表記を変えたい場合は report renderer の責務とする。

### 8.6 Omit

中間 segment と末尾 segment の両方を省略可能とする。

```text
DocumentWrapper.Root.Child1[0].Value
    ↓ DocumentWrapper と Value を省略
Root.Child1[0]
```

末尾 segment を省略した場合、`ProjectedPath` と `ProjectedParentPath` が同じ文字列になることがある。

利用側定義 path は node lookup 用の住所ではないため、これを許容する。

### 8.7 空 path の拒否

すべての segment が `Omit` され、`ProjectedPath` が空になる場合は `InvalidOperationException` とする。

空文字を有効な利用側定義 path として返さない。

### 8.8 重複 path

複数の entry が同じ `ProjectedPath` になることを許容する。

```text
Root.Item.Value
Root.Item.Value
```

SSC は利用側定義 path の一意性を保証しない。

理由:

- 投影は表示と filter のための機能である
- entry は `Entry.Path` により標準位置を保持している
- 同名 node の区別規則は利用側固有である

filter は一致したすべての entry を返す。

### 8.9 Model slot 間の値

SSC は複数 model slot のどの値を採用するか決めない。

投影器は次を使って判断する。

- `IParallelNode.Count`
- `IParallelNode.GetValue()`
- `IParallelNode.GetState()`

推奨例:

1. `Missing` ではない slot から候補名を取得する
2. すべて同じ場合だけその名前を使う
3. 異なる場合は `KeepStandard()` へ fallback する

この規則自体は SSC の production code へ実装しない。

### 8.10 Container presence entry

empty container の有無差分では対応する child node が存在しない。

その場合の context は次とする。

```text
Current.Node       = null
Current.Siblings   = empty
Current.ParentNode = container owner node
```

node 値を必要とする投影器は通常 `KeepStandard()` を返す。

### 8.11 投影器の呼び出し

投影器は同期的に呼び出す。

初期実装では並列呼び出しを行わない。

同じ node に対応する segment が複数 entry の path に現れる場合、投影器が複数回呼ばれる可能性がある。

投影器は呼び出し回数へ依存しない実装とする。

## 9. 利用例

### 9.1 比較 model

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

次のコードは利用方法を示す概念例であり、SSC が domain 固有投影器として提供するものではない。

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

### 9.3 Filter

```csharp
IReadOnlyList<ParallelDiffEntryPathProjection> projections =
    result.GetDiffEntryPathProjections(new NamedTreePathProjector());

ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
    "Root.Child1[*].Child2[*].Attribute1[*].Value");

ParallelDiffEntryPathProjection[] matched = projections
    .Where(projection => projection.PathMatches(pattern))
    .ToArray();
```

標準 path に対する既存 filter も同時に利用できる。

```csharp
ParallelDiffEntry[] standardMatches = result
    .GetDiffEntries()
    .Where(entry => entry.PathMatches(standardPattern))
    .ToArray();
```

## 10. 例外と失敗

- `GetDiffEntryPathProjections(null result, ...)` は `ArgumentNullException`
- `GetDiffEntryPathProjections(..., null projector)` は `ArgumentNullException`
- `ParallelDiffPathSegment.Member(null)` は `ArgumentNullException`
- `ParallelDiffPathSegment.Member(空文字)` は `ArgumentException`
- `ParallelDiffPathSegment.Key(..., null)` は `ArgumentNullException`
- `ParallelDiffPathSegment.Key(..., 空文字)` は `ArgumentException`
- `ParallelDiffPathSegment.Ordinal(..., 負数)` は `ArgumentOutOfRangeException`
- `Replace(null)` は `ArgumentNullException`
- 全 segment を省略した場合は `InvalidOperationException`
- 投影器が送出した例外は握りつぶさず、呼び出し元へ伝播する
- 不正な member 名は segment factory の validation で拒否する

## 11. 実装方針

### 11.1 比較 tree の1回走査

標準差分 entry と利用側定義 path は、同じ `DiffEntryCollector` の再帰走査から生成する。

標準 path を文字列から再解析しない。

`GetNodeByPath()` で root から再探索しない。

### 11.2 Path frame

再帰中は path 階層ごとに次を保持する。

```text
StandardSegment
ParentNode
Node
Siblings
```

この1階層分の情報を path frame として stack 状に保持する。

`stack` は、現在の再帰位置までの要素を順番に積み上げた構造を意味する。

```text
Root
Root + Children[0]
Root + Children[0] + Fields[0]
Root + Children[0] + Fields[0] + Value
```

### 11.3 標準 entry の生成

標準 entry の生成時は frame 内の標準 segment を formatter へ渡す。

```text
frames[0..last]     -> Entry.Path
frames[0..last - 1] -> Entry.ParentPath
```

既存 path の文字列結果を維持する。

### 11.4 利用側定義 path の生成

各 frame を `ParallelDiffPathNodeContext` へ変換し、root 側から投影器へ渡す。

投影結果を具体 segment の列へ変換し、同じ formatter で文字列化する。

```text
KeepStandard -> StandardSegment
Replace      -> Replacement
Omit         -> segment を追加しない
```

### 11.5 Formatter の共有

標準 path と利用側定義 path は `ParallelDiffPathFormatter` を共有する。

formatter は次を担当する。

- dot 連結
- `[]` の生成
- key escape
- 先頭 `#` の escape
- ordinal の `#` 表記
- invariant culture による ordinal 文字列化

標準 path と利用側定義 path で異なる grammar を作らない。

### 11.6 Pattern parser の再利用

利用側定義 path は既存 path grammar で文字列化するため、`ParallelDiffPathPattern` の既存 parser と matcher を再利用する。

新しい wildcard grammar は追加しない。

### 11.7 比較 pipeline との分離

本機能の production code から次を変更しない。

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
  -> GetDiffEntries()                         標準 entry
  -> GetDiffEntryPathProjections(projector)  利用側定義 path 付き結果
```

## 12. 後方互換

### 12.1 既存 API

次の API の signature と挙動を変更しない。

```csharp
result.GetDiffEntries();
entry.PathMatches(pattern);
result.GetNodeByPath(path);
result.GetValueByPath(path, modelIndex);
result.GetStateByPath(path, modelIndex);
```

`signature` は method 名、引数、戻り値からなる公開形式を意味する。

### 12.2 `ParallelDiffEntry.ToString()`

既存どおり `ParallelDiffEntry.Path`、つまり標準 path を表示する。

利用側定義 path を暗黙に使用しない。

### 12.3 標準 path の解決可能性

次の既存契約を維持する。

```csharp
entry.Node == result.GetNodeByPath(entry.Path)
```

利用側定義 path にはこの契約を適用しない。

### 12.4 比較結果への非影響

投影前後で次を変更しない。

- `CompareResult<T>.Root`
- `CompareResult<T>.Issues`
- `CompareResult<T>.HasError`
- `Root.HasDifferences()`
- 標準 diff entry の件数と順序

## 13. テスト方針

### 13.1 Segment contract

- member segment を生成できる
- key selector segment を生成できる
- ordinal selector segment を生成できる
- `WithMemberName()` が selector を維持する
- 空 member 名を拒否する
- `.`, `[`, `]` を含む member 名を拒否する
- 空 key text を拒否する
- 負の ordinal を拒否する

### 13.2 Segment projection

- `KeepStandard()` が標準 segment を維持する
- `Replace()` が member 名を変更できる
- `Replace()` が key selector を維持できる
- `Replace()` が ordinal selector を維持できる
- `Omit()` が中間 segment を省略できる
- `Omit()` が末尾 segment を省略できる
- 全 segment の `Omit()` を拒否する

### 13.3 Context

- root 直下では ancestor が空になる
- nested node では ancestor が root 順に渡る
- scalar/object member で `Node` が渡る
- container element で sibling 一覧が渡る
- key child で標準 selector が `[key]` になる
- ordinal child で標準 selector が `[#n]` になる
- container presence entry で `Node == null`、siblings が空になる

### 13.4 Escape

- key text 内の `]` を escape する
- key text 内の `\` を escape する
- key text 先頭の `#` を escape する
- 不正な member 名から不正 path を生成しない

### 13.5 Pattern matching

- projection extension が `ProjectedPath` を照合する
- 既存 entry extension が標準 `Path` を照合し続ける
- `[*]` が projected key selector に一致する
- `[*]` が projected ordinal selector に一致する
- exact key と exact ordinal を区別する

### 13.6 重複と fallback

- 同じ `ProjectedPath` を持つ複数 entry を保持する
- 投影器が `KeepStandard()` を返した segment だけ標準名へ戻る
- 一部 segment だけ置換できる
- 投影器例外を呼び出し元へ伝播する

### 13.7 実比較 E2E

再帰 model を実際に `ParallelCompareApi.Compare()` へ渡し、次を確認する。

```text
標準 path:
Root.Children[0].Children[0].Fields[0].Value

利用側定義 path:
Root.Child1[0].Child2[0].Attribute1[0].Value
```

あわせて次を確認する。

- 標準 path で node を解決できる
- 利用側定義 pattern が projected path に一致する
- 同じ pattern が標準 path には一致しない
- 投影呼び出し前後で標準 entry が変化しない

### 13.8 既存回帰

- options なしの `GetDiffEntries()` が既存 entry 件数を維持する
-標準 `Path` と `ParentPath` が完全一致する
- entry 順序が変わらない
- `Entry.Node` が `GetNodeByPath(Entry.Path)` で解決できる
- `ParallelDiffEntry.ToString()` が変わらない
- 既存 `ParallelDiffPathPattern` test を変更せず通す
- 既存 `CompareIgnore` test を変更せず通す

## 14. 実装対象ファイル

```text
src/SSC/
  ParallelDiffPathProjection.cs
  ParallelDiffPathSegments.cs
  ParallelPathAccessExtensions.cs

src/SSC/Internal/
  ParallelDiffPathFormatter.cs

tests/SSC.Unit.Tests/
  ParallelDiffPathProjectionUnitTests.cs

tests/SSC.E2E.Tests/
  ParallelDiffPathProjectionE2ETests.cs

doc/design/detail/
  11-DiffEntryCustomPath.md
```

## 15. 完了条件

- 既存 `ParallelDiffEntry.Path` が標準 path として維持される
- 利用側が node context から segment を置換または省略できる
- 利用側が path 文字列を手作業で組み立てる必要がない
- 再帰 model でも実行時の意味名を使った利用側定義 path を生成できる
- 利用側定義 path を既存 `ParallelDiffPathPattern` で絞り込める
- XML 等の domain 固有規則が SSC production code に入らない
- `CompareConfiguration`、metadata resolution、比較 tree 構築へ影響しない
- 既存 path access、diff entry、filter の全回帰が通る
