# Diff Entry Custom Path Projection

## 1. 目的

`CompareResult<T>.GetDiffEntries()` が返す差分 entry に対し、SSC が生成する既存の path を変更せず、利用側が用途に応じた別の path 表現を生成できるようにする。

再帰的なモデルでは、既存 path が次のように同じ member 名を繰り返すことがある。

```text
Root.Children[#0].Children[#0].Fields[#0].Value
```

利用側が各 node の `Name` などの実行時の値を使い、次のような読みやすい path を生成できることを目的とする。

```text
Root.Child1[#0].Child2[#0].Attribute1[#0].Value
```

本機能は path の表示と絞り込みを改善するための後段処理であり、比較対象、比較結果、sequence の対応付けには影響しない。

## 2. 用語

### 2.1 標準 path（canonical path）

標準 path は、SSC が現在 `ParallelDiffEntry.Path` に格納している正式な path を指す。

`canonical` は「基準となる正式な表現」という意味である。本設計では英語だけで呼ばず、以降は原則として「標準 path」と記載する。

例:

```text
Groups[1].Items[100].MetricA
Root.Children[#0].Fields[#0].Value
```

標準 path は SSC の比較 tree 上の位置を表す。

- `GetNodeByPath()` で node を取得できる
- `ParallelDiffEntry.Path` と `ParallelDiffEntry.ParentPath` に使用される
- `ParallelDiffEntry.PathMatches()` の照合対象になる
- 既存 API の後方互換対象である

### 2.2 利用側定義 path（custom path）

利用側定義 path は、利用側が標準 path を別の意味表現へ変換した path を指す。

`custom` は「利用側が用途に合わせて定義する」という意味である。

例:

```text
Standard path:
Root.Children[#0].Fields[#0].Value

Custom path:
Root.Child1[#0].Attribute1[#0].Value
```

利用側定義 path は表示、分類、filter に使用する。初期実装では node の住所としては扱わない。

### 2.3 別名（alias）

`alias` は「同じ対象に付ける別の名前」を意味する。

例えば `Children` を `Child1` と表示する場合、`Child1` は `Children` segment の別名に相当する。

ただし本設計では、固定文字列の別名だけでなく実行時の node 値から名前を決めるため、公開 API の中心用語には `alias` ではなく `projection` を使う。

### 2.4 投影（projection）

`projection` は「元の情報を保持しながら、必要な見え方へ変換する処理」を意味する。

本設計では、標準 path の各構成要素を次のいずれかへ変換する処理を指す。

- 標準のまま維持する
- 別の構成要素へ置き換える
- 利用側定義 path から省略する

標準 path 自体は変更しない。

### 2.5 投影器（projector）

`projector` は projection を実行する利用側実装を指す。

SSC は node と path の構造情報を projector に渡す。projector は、その情報から利用側定義 path の構成要素を返す。

SSC は `Name`、`Children`、`Attributes` などの業務上の意味を解釈しない。

### 2.6 区間（segment）

`segment` は dot で区切られた path の構成要素1件を指す。

例:

```text
Root.Children[#0].Value
```

この path は次の3 segment で構成される。

```text
Root
Children[#0]
Value
```

### 2.7 選択子（selector）

`selector` は、container member 配下の要素を識別する `[]` 部分を指す。

例:

```text
Items[A]
Items[#0]
```

- `[A]` は key selector。比較 key の文字列表現 `A` で要素を識別する
- `[#0]` は ordinal selector。key を持たない sequence の並び順 `0` で要素を識別する

具体的な path segment では wildcard selector `[*]` を使用しない。`[*]` は pattern 側だけの表現とする。

### 2.8 文脈情報（context）

`context` は、projector が判断に使う現在位置の情報を指す。

本設計では次の情報を含む。

- 現在の標準 path segment
- 親 node
- 現在の node
- 同じ child set に属する sibling node 一覧
- 現在位置より上位の ancestor 情報

`sibling` は「同じ親を持つ兄弟 node」、`ancestor` は「現在位置より上位にある祖先 node」を意味する。

### 2.9 フォールバック（fallback）

`fallback` は「利用側が別名を決定できない場合に、安全な既定動作へ戻すこと」を意味する。

本設計で推奨する fallback は、該当 segment を標準 path のまま維持することである。

```text
Children[#0]
    ↓ 別名を決定できない
Children[#0]
```

## 3. 現状と問題

現行 `GetDiffEntries()` は、比較 tree を再帰的に走査し、member 名と selector を連結して `ParallelDiffEntry.Path` を生成する。

container child では次の規則を使用する。

- `IParallelNode.KeyText` がある場合は key selector
- `IParallelNode.KeyText` がない場合は ordinal selector

そのため、汎用的な再帰モデルでは次のような path になる。

```text
Root.Children[#0].Children[#0].Fields[#0].Value
```

この path は SSC の model 構造としては正しいが、利用者が知りたい意味を直接表さない場合がある。

例えば次の model を考える。

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

しかし SSC が `Name` を path 名として自動解釈することはできない。

- `Name` が path 名であるとは限らない
- 別の model では `Code`、`Id`、`Label` などが意味名かもしれない
- model slot 間で値が異なる場合の採用規則は利用側ごとに異なる
- 同名 sibling の識別規則も利用側ごとに異なる

SSC は判断材料を渡し、意味の決定は利用側に委ねる必要がある。

## 4. 責務境界

### 4.1 SSC の責務

SSC は次を担当する。

1. 既存の標準 path を変更せず生成する
2. 標準 path を構造化された segment と selector として扱う
3. path 生成時に把握している node context を projector へ渡す
4. projector の結果を SSC の escape 規則で path 文字列へ変換する
5. 標準 path と利用側定義 path を同じ結果オブジェクトから参照できるようにする
6. 利用側定義 path に対して既存 `ParallelDiffPathPattern` を適用できるようにする
7. 既存 `GetDiffEntries()` の件数、順序、path、node 参照を維持する

### 4.2 利用側の責務

利用側は次を担当する。

1. どの member を置換または省略するか決める
2. node のどの実行時の値を利用側定義名として使うか決める
3. model slot 間で候補名が異なる場合の規則を決める
4. 同名 sibling をどう区別するか決める
5. 名前を決定できない場合に標準 segment へ戻すか、別の名前を使うか決める
6. 利用側定義 path を report、log、分類へどう表示するか決める

### 4.3 SSC が解釈しない情報

SSC は次の意味を自動判定しない。

```text
Name        = node 名
Children    = XML の子要素
Fields      = 属性
Value       = report では省略可能
```

これらは model 固有または利用用途固有の規則である。

## 5. 非目的

初期実装では次を行わない。

- `ParallelDiffEntry.Path` の意味変更
- `ParallelDiffEntry.ParentPath` の意味変更
- `GetNodeByPath()` による利用側定義 path の解決
- `CompareConfiguration` への path projector 設定追加
- 比較対象 member の変更
- `CompareKey` の代替
- sequence alignment の変更
- `CompareIssue.Path` の projection
- XML、JSON、tree model 固有の projector 実装
- property 名から `Name` や `Id` を自動発見する規則
- 利用側定義 path の一意性保証
- member 名 wildcard や任意深度 wildcard の追加
- report 用の `[#0]` から `[0]` への表記変換
- 正規表現による path 変換

## 6. 固定 alias attribute を対象外とする理由

### 6.1 alias attribute の想定例

`attribute` は C# の class、property、field などへ付加する metadata を意味する。

固定 alias attribute を導入する場合、概念的には次のような API になる。

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

この場合、次の固定置換は可能である。

```text
Items[100].Price
    ↓
Lines[100].Price
```

### 6.2 今回の要求を満たせない理由

再帰 model で必要になる名前は、property に固定された名前ではなく各 node の実行時の値である。

```csharp
public sealed class TreeNode
{
    public string Name { get; init; } = string.Empty;

    public List<TreeNode> Children { get; init; } = [];
}
```

同じ `Children` property の各要素が次の異なる名前を持つ。

```text
Child1
Child2
Child3
```

次のような固定 attribute では表現できない。

```csharp
[DiffPathAlias("Child1")]
public List<TreeNode> Children { get; init; }
```

この指定ではすべての child が `Child1` になってしまう。

### 6.3 初期設計での判断

固定 alias attribute は初期対象に含めない。

理由:

- 実行時の node 値を参照できない
- `TypeMetadataResolver` へ別名 metadata の責務が増える
- 比較 metadata と出力表現の責務が混ざる
- 同じ比較結果へ異なる path 表現を適用しにくくなる

固定 member rename の需要が別途明確になった場合は、本 projection API の簡易 projector として後続設計する。

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

### 7.2 Projector

```csharp
public interface IParallelDiffPathProjector
{
    ParallelDiffPathSegmentProjection Project(
        ParallelDiffPathProjectionContext context);
}
```

`Project()` は標準 path の segment 1件に対し、利用側定義 path での扱いを返す。

projector は同じ入力に対して同じ結果を返す決定的な実装とする。

`deterministic` または「決定的」とは、同じ入力なら毎回同じ結果になる性質を意味する。時刻、乱数、外部の可変状態へ依存させない。

### 7.3 Projection context

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
| `Node` | segment が指す現在の node。container presence entry では `null` |
| `Siblings` | 同じ `ParallelChildSet` に属する node 一覧。container presence entry では空 |

### 7.4 Concrete path segment

`concrete` は「wildcard ではなく、特定の位置を表す具体的な値」という意味である。

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

例:

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
public sealed class ParallelDiffPathSelector
{
    public ParallelDiffPathSelectorKind Kind { get; }

    public string? KeyText { get; }

    public int? Ordinal { get; }
}
```

path segment factory が selector の整合性を保証する。

- `Key()` は `KeyText` を持つ
- `Ordinal()` は非負の `Ordinal` を持つ
- wildcard は持たない

### 7.6 Segment projection result

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

### 7.7 Projection result

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
| `Entry` | 既存の標準差分 entry |
| `Entry.Path` | SSC が生成した標準 path |
| `ProjectedPath` | projector を適用した利用側定義 path |
| `ProjectedParentPath` | 標準 parent path と同じ segment 範囲へ projector を適用した path |

利用例:

```csharp
ParallelDiffEntryPathProjection projected = projections[0];

string standardPath = projected.Entry.Path;
string customPath = projected.ProjectedPath;
```

結果例:

```text
standardPath:
Root.Children[#0].Fields[#0].Value

customPath:
Root.Child1[#0].Attribute1[#0].Value
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

既存 overload は変更しない。

```csharp
entry.PathMatches(pattern);
```

既存 overload は引き続き `ParallelDiffEntry.Path`、つまり標準 path を照合する。

利用例:

```csharp
ParallelDiffPathPattern pattern = ParallelDiffPathPattern.Parse(
    "Root.Child1[*].Child2[*].Attribute1[*].Value");

ParallelDiffEntryPathProjection[] matched = result
    .GetDiffEntryPathProjections(projector)
    .Where(entry => entry.PathMatches(pattern))
    .ToArray();
```

## 8. Projection 規則

### 8.1 標準 path の維持

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

### 8.2 Segment 単位の処理

標準 path を root 側から segment 単位で処理する。

```text
Root
Children[#0]
Fields[#0]
Value
```

各 segment に対して projector を適用し、結果を順番に連結する。

### 8.3 Escape

利用側が返す member 名と key text は、SSC が既存 path grammar に従って escape する。

利用側は文字列連結で path を作らない。

例:

```text
Member name: A.B
Key text:    A]B
```

これらを path として安全に表現する責務は SSC が持つ。

既存 grammar が member 名の dot escape を許容していない場合は、初期実装で対応可能な member 名の範囲を明示し、不正な member 名を拒否する。暗黙に不正 path を生成しない。

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

report 上だけ `[0]` と表示したい場合は report renderer の責務とする。

### 8.5 Omit

中間 segment と末尾 segment の両方を省略可能とする。

例:

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

複数の entry が同じ `ProjectedPath` になることを許容する。

例:

```text
Root.Item.Value
Root.Item.Value
```

SSC は利用側定義 path の一意性を保証しない。

理由:

- projection は表示と filter のための機能である
- entry は `Entry.Path` により標準位置を保持している
- 同名 node の区別規則は利用側固有である

filter は一致したすべての entry を返す。

### 8.8 Model slot 間の値

SSC は複数 model slot のどの値を採用するか決めない。

projector は `IParallelNode.Count`、`GetValue()`、`GetState()` を使って判断する。

推奨例:

1. `Missing` ではない slot から候補名を取得する
2. すべて同じ場合だけその名前を使う
3. 異なる場合は `KeepStandard()` へ fallback する

ただし、この規則自体は SSC の production code へ実装しない。

### 8.9 Container presence entry

empty container の有無差分では、対応する child node が存在しない。

その場合の context は次とする。

```text
Current.Node     = null
Current.Siblings = empty
Current.ParentNode = container owner node
```

projector は node 値を必要とする置換を行えないため、通常は `KeepStandard()` を返す。

### 8.10 Projector の呼び出し

projector は同期的に呼び出す。

初期実装では並列呼び出しを行わない。

同じ node に対応する segment が複数 entry の path に現れる場合、projector が複数回呼ばれる可能性がある。projector は呼び出し回数へ依存しない実装とする。

## 9. 利用例

### 9.1 Named tree model

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
Root.Children[#0].Children[#0].Fields[#0].Value
```

利用側が目指す path:

```text
Root.Child1[#0].Child2[#0].Attribute1[#0].Value
```

### 9.2 利用側 projector の概念例

次のコードは利用方法を示す概念例であり、SSC が提供する具体的 projector ではない。

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
- `ParallelDiffPathSegment.Member(null または空文字)` は `ArgumentException`
- `ParallelDiffPathSegment.Key(..., null または空文字)` は `ArgumentException`
- `ParallelDiffPathSegment.Ordinal(..., 負数)` は `ArgumentOutOfRangeException`
- `Replace(null)` は `ArgumentNullException`
- 全 segment を省略した場合は `InvalidOperationException`
- projector が送出した例外は握りつぶさず、そのまま呼び出し元へ伝播する
- projector が不正な path segment を返すことは factory validation により防ぐ

`validation` は入力値が API 契約を満たすか検査することを意味する。

## 11. 実装方針

### 11.1 標準 path の segment 化

現行 `AddNodeDiffEntries()` と `AddChildSetDiffEntries()` が文字列 path を直接連結する処理を、内部の segment stack を使う形へ整理する。

`stack` は「現在の再帰位置までの要素を順番に積み上げた構造」を意味する。

概念:

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

差分 entry を生成する時点で、この stack を root から順に projector へ渡す。

標準 path 文字列を再解析しない。

`GetNodeByPath()` で root から node を再探索しない。

### 11.3 Formatter の共有

標準 path と利用側定義 path は同じ formatter を使用する。

`formatter` は構造化された segment を文字列へ変換する処理を意味する。

次を共有する。

- dot 連結
- `[]` の生成
- key escape
- 先頭 `#` の escape
- ordinal の `#` 表記

標準 path と利用側定義 path で異なる grammar を作らない。

### 11.4 Pattern parser の再利用

利用側定義 path は既存 path grammar で文字列化するため、`ParallelDiffPathPattern` の既存 parser と matcher を再利用する。

新しい wildcard grammar は追加しない。

### 11.5 比較 pipeline との分離

本機能の production code から次を変更しない。

- `ParallelCompareApi.Compare()`
- `CompareConfiguration`
- `CompareIgnoreAttribute`
- `CompareKeyAttribute`
- `TypeMetadataResolver`
- container normalization
- key union
- node の value/state 判定

`pipeline` は入力から比較結果を生成する一連の処理を意味する。

本機能は比較結果の列挙時にだけ動作する。

```text
models
  -> metadata resolution
  -> comparison tree construction
  -> CompareResult<T>
  -> GetDiffEntries()                     標準 entry
  -> GetDiffEntryPathProjections(projector) 利用側定義 path 付き結果
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

## 13. テスト方針

### 13.1 既存回帰

- options なしの `GetDiffEntries()` が既存 entry 件数を維持する
- 標準 `Path` と `ParentPath` が完全一致する
- entry 順序が変わらない
- `Entry.Node` が `GetNodeByPath(Entry.Path)` で解決できる
- `ParallelDiffEntry.ToString()` が変わらない
- 既存 `ParallelDiffPathPattern` test を変更せず通す
- 既存 `CompareIgnore` test を変更せず通す

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
- ordinal child で標準 selector が `[#n]` になる
- key child で標準 selector が `[key]` になる
- container presence entry で `Node == null`、siblings が空になる

### 13.4 Escape

- key text 内の `]` を escape する
- key text 内の `\` を escape する
- key text 先頭の `#` を escape する
- projector が返した member 名の許容範囲を検証する
- 不正な member 名から不正 path を生成しない

### 13.5 Pattern matching

- projection extension が `ProjectedPath` を照合する
- 既存 entry extension が標準 `Path` を照合し続ける
- `[*]` が projected key selector に一致する
- `[*]` が projected ordinal selector に一致する
- exact key と exact ordinal を区別する

### 13.6 重複と fallback

- 同じ `ProjectedPath` を持つ複数 entry を保持する
- projector が `KeepStandard()` を返した segment だけ標準名へ戻る
- 一部 segment だけ置換できる
- projector 例外を呼び出し元へ伝播する

### 13.7 比較結果への非影響

- `CompareResult<T>.Root` が変化しない
- `CompareResult<T>.Issues` が変化しない
- `CompareResult<T>.HasError` が変化しない
- `Root.HasDifferences()` が変化しない
- `GetDiffEntries()` の結果が projection 呼び出し前後で変化しない

## 14. 実装対象ファイルの想定

初期実装では概ね次を対象とする。

```text
src/SSC/
  ParallelDiffPathProjection.cs
  ParallelDiffPathSegments.cs
  ParallelPathAccessExtensions.cs

src/SSC/Internal/
  path formatter / traversal context helper

tests/SSC.Unit.Tests/
  ParallelDiffPathProjectionUnitTests.cs

tests/SSC.E2E.Tests/
  ParallelDiffPathProjectionE2ETests.cs

doc/design/detail/
  02-PublicApi.md
  11-DiffEntryCustomPath.md
```

実装時の class 分割は既存の source 配置と責務に合わせて調整する。

## 15. 完了条件

- 既存 `ParallelDiffEntry.Path` が標準 path として維持される
- 利用側が node context から segment を置換または省略できる
- 利用側が path 文字列を手作業で組み立てる必要がない
- 再帰 model でも意味名を使った利用側定義 path を生成できる
- 利用側定義 path を既存 `ParallelDiffPathPattern` で絞り込める
- XML 等の domain 固有規則が SSC production code に入らない
- `CompareConfiguration`、metadata resolution、比較 tree 構築へ影響しない
- 既存 path access、diff entry、filter の全回帰が通る
