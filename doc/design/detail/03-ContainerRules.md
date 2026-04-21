# Container Mapping Rules

## 1. Supported Containers

1. Keyed: `IDictionary<TKey,TValue>`, `IReadOnlyDictionary<TKey,TValue>`
2. Sequence: `IList<T>`, `IReadOnlyList<T>`, `T[]`
3. Sequence-like: `IEnumerable<T>`（実行時解決可能な場合）

## 2. Dictionary Rules

- `TKey` を比較キーとして使用（`CompareKey` 不要）
- 全 model から keyUnion を作成
- key ごとに `Parallel<TValue>` を作成
- 同一 model 内で比較器上の同値キーが再出現した場合は `DuplicateCompareKeyDetected`

例:

```text
data0 = {A:80, B:70}
data1 = {A:90, C:60}

union = [A,B,C]
E0=[80,90], E1=[70,null], E2=[null,60]
```

## 3. List/Array Rules

- 要素型に `CompareKey` が必須
- `CompareKey` 無し: `Skip + CompareKeyNotFoundOnSequenceElement`
- 重複キー: `DuplicateCompareKeyDetected`
- strict モードでは上記を例外化
- trace 有効時は declared type に対して `List` / `Array` のどちらで扱ったかを記録する

例:

```text
data0: (1,10), (2,20)
data1: (1,11), (3,30)

union=[1,2,3]
E0=[(1,10),(1,11)]
E1=[(2,20),null]
E2=[null,(3,30)]
```

## 4. IEnumerable Rules

- Compare 開始時に `List<T>` へ 1 回マテリアライズ
- 再列挙しない
- 実行時型が未対応コンテナの場合 `UnsupportedContainerType`
- trace 有効時は declared type と runtime type、materialize 件数、再判定結果を記録する

## 5. Unsupported Containers

- `IAsyncEnumerable<T>`
- one-shot 列挙体（再実行不能）

## 5.1 Trace Expectations

trace 有効時、container 判定では少なくとも次のような行が出力される。

```text
phase=metadata path=Dataset.Items declaredType=System.Collections.Generic.List<Item> container=List elementType=Item
phase=container path=Dataset.Items modelIndex=0 runtimeType=System.Collections.Generic.List<Item> materializedCount=3 compareKey=Id
```

`IEnumerable<T>` 宣言プロパティでは、`container=IEnumerable` と runtime 側の実体型を分けて記録する。

## 6. Key Order Rule

- keyUnion は決定論的順序
- 文字列キー比較は `CompareConfiguration.StringKeyComparison` に従う（既定: `Ordinal`）
- `OrdinalIgnoreCase` 時は大文字小文字差のみのキーを同一キーとして扱う
- `OrdinalIgnoreCase` 時の `KeyText` は同値候補のうち `StringComparer.Ordinal` で最小の表記を採用する
- 既定比較器で比較不能なキーは Error

## 7. Key Comparison Examples

### 7.1 String Key (`Ordinal`, default)

```text
data0 = {"a": 1}
data1 = {"A": 2}

Ordinal 比較:
union = ["A", "a"]   // 別キーとして扱う
```

### 7.2 String Key (`OrdinalIgnoreCase`)

```text
data0 = {"a": 1}
data1 = {"A": 2}

OrdinalIgnoreCase 比較:
union = ["A"]  // 同一キーとして統合
```

注記:

- 表示用 `KeyText` は同値候補の Ordinal 最小表記を採用する（例: `"A"`）。

### 7.3 DateTime Key

```text
data0 = {2026-04-03T00:00:00Z: X}
data1 = {2026-04-03T09:00:00+09:00: Y}

UTC 正規化後に同一点なら同一キーとして扱う
```

### 7.4 Composite Key

```text
key = (GroupId, ItemId)

比較順:
1) GroupId
2) ItemId
```

意味:

- 「一致判定」は `GroupId` と `ItemId` の両方が同じときだけ一致
- 「順序固定」はキー部品の並び順を固定すること
  - 正: `(GroupId, ItemId)`
  - 誤: `(ItemId, GroupId)` を別定義として混在

具体例:

```text
data0 key: (10, 1), (10, 2), (20, 1)
data1 key: (10, 1), (20, 1), (20, 2)

一致:
- (10, 1)
- (20, 1)

欠損:
- data0 側のみ: (10, 2)
- data1 側のみ: (20, 2)
```

要するに「複合キーの列定義そのもの」を固定し、
比較時はその固定順で判定する。

結論:

- 複合キーは、構成するキーがすべて一致しない限り一致として扱わない。
