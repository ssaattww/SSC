# T-090 polymorphic sequence runtime 型比較 設計メモ

## 背景

`IEnumerable<Item>` のように基底型で宣言された sequence に、実行時には `Node` や `Content` などの派生型が格納されるモデルを比較すると、派生型固有メンバーの差分が `GetDiffEntries()` に現れない。

確認した最小構造は次のとおり。

```csharp
public class Item
{
}

public sealed class Node : Item
{
    public string Name { get; init; } = string.Empty;

    public IEnumerable<Item> Children { get; init; } = [];
}

public sealed class Content : Item
{
    public string Text { get; init; } = string.Empty;
}
```

この構造で左右の同じ ordinal に `Content` があり、`Text` だけが異なっていても、従来実装では差分 entry が 0 件になる。

## 問題が起きる理由

sequence 構築処理は、メンバー宣言から得た要素型 `Item` をそのまま `BuildNode` に渡している。

```text
declaredType = IEnumerable<Item>
elementType  = Item
nodeType     = Item
```

`BuildNodeGeneric<TNode>` は `typeof(TNode)` に対して比較対象メンバーを列挙する。そのため `TNode == Item` で、`Item` に public property / field が無い場合、子 node は比較対象メンバーを一つも持たない。

実際の slot 値が `Node` または `Content` でも、次のメンバーは比較木に登録されない。

- `Node.Name`
- `Node.Children`
- `Content.Text`
- その他の派生型固有メンバー

これは parser、`CompareKey`、`GetDiffEntries()` の表示フィルターによる欠落ではなく、比較木を構築する段階で派生型情報を参照していないことが原因である。

## 修正方針

### 1. 公開 node 型とメンバー探索型を分離する

公開される node の generic 型は、従来どおり sequence の宣言要素型を維持する。

```text
ParallelNode<Item>
```

一方、比較対象メンバーの探索には、aligned slot 内の runtime 型を使用できるようにする。

```text
nodeType       = Item
comparisonType = Content
```

これにより `GetChildren<Item>(...)` など既存の型付きアクセスを壊さず、`Content.Text` や `Node.Name` を比較木へ登録できる。

### 2. aligned slot の runtime 型が一種類の場合

`PresentValue` の slot から runtime 型を収集し、全て同一型で、かつ宣言要素型へ代入可能な場合、その型を `comparisonType` とする。

```text
Node    vs Node    -> comparisonType = Node
Content vs Content -> comparisonType = Content
Node    vs Missing -> comparisonType = Node
Node    vs null    -> comparisonType = Node
```

`Missing` と `PresentNull` は runtime 型解決の候補から除外し、既存の presence/null 判定を維持する。

### 3. aligned slot に複数の runtime 型が混在する場合

次のような比較では、どちらか一方の派生型へ強制キャストしない。

```text
Node vs Content
```

この場合は以下の契約とする。

1. 要素 node 自身を `Mismatched` とする。
2. `HasDifferences()` は `true` を返す。
3. `GetDiffEntries()` は、その要素 path に node entry を1件生成する。
4. 派生型固有メンバーの再帰比較は打ち切る。
5. `CompareIssue` は追加せず、`HasError` も立てない。

型違いは入力エラーではなく、通常の構造差分として扱う。

派生型ごとの全メンバーを union して `Missing` 差分へ展開する方式は採用しない。型変更1件が大量の派生メンバー差分へ膨らみ、差分の主因が不明瞭になるためである。

### 4. null / Missing の既存契約

| 左 | 右 | 結果 |
|---|---|---|
| `Node` | `Node` | `Node` のメンバーを再帰比較 |
| `Content` | `Content` | `Content` のメンバーを再帰比較 |
| `Node` | `Content` | 要素 node の runtime 型差分 |
| `Node` | `null` | 既存の value/null presence 差分 |
| `Node` | `Missing` | 既存の presence 差分 |
| `null` | `null` | 一致 |
| `Missing` | `Missing` | 比較対象なし |

## 実装対象

### `ParallelCompareApi`

- `BuildNode` に、generic node 型とは別の `comparisonType` を渡せる内部経路を追加する。
- `BuildNodeGeneric<TNode>` は scalar 判定と comparable member 探索に `comparisonType` を使用する。
- keyed sequence と ordinal-aligned sequence の child slot 構築後に runtime 型を解決する。
- public node の generic 型には宣言要素型を使用し続ける。
- trace に `nodeType`、`comparisonType`、runtime 型不一致を識別できる情報を追加する。

### `ParallelNode<T>`

- `PresentValue` slot の runtime 型不一致を検出する。
- runtime 型不一致時は各 present slot の `GetState()` を `Mismatched` とする。
- runtime 型不一致時は `HasDifferences()` を `true` とする。
- runtime 型不一致 node は派生メンバーへ再帰せず、要素自身を leaf 相当の差分として公開する。

## 変更しない範囲

- sequence の alignment 規則は変更しない。
  - `[CompareKey]` がある場合は key union。
  - `[CompareKey]` がない場合は ordinal index。
- `MissingCompareKeyListPolicy` の意味は変更しない。
- runtime 派生型にだけ存在する `[CompareKey]` を新たに探索する仕様は本タスクに含めない。
- public enum や public interface へのメンバー追加は行わない。

## 必須テスト

1. 空の基底型で宣言された sequence に、左右とも同じ派生型が入り、派生型scalar memberだけが異なる場合に差分が出る。
2. `Node.Children : IEnumerable<Item>` のような再帰構造で、孫の `Content.Text` 差分まで検出できる。
3. 同じ ordinal で `Node` と `Content` が対向した場合、要素 path に1件の runtime 型差分が出て再帰しない。
4. `Node` 対 `null` が既存の null 差分になる。
5. `Node` 対 `Missing` が既存の presence 差分になる。
6. child node が引き続き `ParallelNode<Item>` として取得できる。
7. keyed sequence の key union と keyless sequence の ordinal alignment に回帰がない。
8. dynamic container materialization でも同じ規則が適用される。

## 受け入れ条件

- `IEnumerable<Item>` 配下の同一派生型メンバー差分が `GetDiffEntries()` に現れる。
- runtime 型が異なる aligned element は例外や Error issueではなく通常差分になる。
- 型不一致 entry は要素 path に集約され、派生型固有メンバー差分へ展開されない。
- `GetChildren<Item>()` 等の既存型付きアクセスが維持される。
- 既存の sequence alignment、null、Missing、duplicate key、strict mode のテストが通る。
