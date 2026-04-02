
# 構造並列化比較エンジン 設計書（DB / DataFrame 観点統合版）

本ドキュメントは、これまでの議論をすべて反映した **最新版の設計書** である。

本エンジンは単なる比較ツールではなく、
**構造化オブジェクトに対する「並列クエリエンジン」**として設計されている。

---

## 1. 設計の目的（再定義）

本エンジンの目的は次の一点に集約される。

> **複数の構造化オブジェクトを、同一構造位置ごとに横に並べ、
> ユーザーが型安全な API（LINQ）だけで自由にクエリ・比較できるようにすること**

- join をユーザーに書かせない
- diff API をユーザーに見せない
- 2 件〜 N 件まで同一設計
- 欠損・失敗があっても壊れない

---

## 2. 核となる設計原理

### 2.1 比較とは「一次元の追加」である

本設計の根幹原理は次である。

> **比較対象となるクラス内のすべてのメンバーは、
> 比較対象数ぶんだけ「一次元増えた配列構造」として扱われる**

これは比喩ではなく、構造・API・実装すべてを貫く厳密な原理である。

---

## 3. 構造変換モデル

### 3.1 通常のオブジェクト構造

```text
Item
 ├ MetricA : double
 ├ Detail  : ItemDetail
 └ Details : List<ItemDetail>
```

### 3.2 比較適用後の概念構造

```text
Parallel<Item>
 ├ MetricA : double[]
 ├ Detail  : Parallel<ItemDetail>
 └ Details : Parallel<ItemDetail>
```

- 構造は変わらない
- 各メンバーが「比較次元」を 1 つ持つ
- この変換は再帰的に適用される

---

## 4. 再帰的リフティング規則

```text
T        → Parallel<T>
List<T> → Parallel<T>
```

```text
class A {
  x : X
  y : Y
}

=== 比較後 ===>

Parallel<A> {
  x : Parallel<X>
  y : Parallel<Y>
}
```

本エンジンは **差分抽出ではなく構造の持ち上げ（Lifting）** を行う。

---

## 5. Parallel<T> 正式インタフェース

```csharp
public interface Parallel<T> : IEnumerable<Parallel<T>>
{
    T? this[int index] { get; }
    int Count { get; }
    bool AllPresent { get; }
    bool AnyPresent { get; }
}
```

- 並列化の抽象は `Parallel<T>` のみ
- `ParallelList<T>` は不要

---

## 6. List マッピング方針（属性ベース）

### 6.1 順序比較の禁止

List を index で揃える比較は**設計上禁止**する。
順序は表示上の情報であり、比較意味を持たない。

---

### 6.2 CompareKey 属性

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class CompareKeyAttribute : Attribute {}
```

List 要素クラスは、自身がどのプロパティで対応付けられるかを宣言する。

```csharp
public sealed class ItemDetail
{
    [CompareKey]
    public int StepNo { get; init; }
    public int ValueX { get; init; }
}
```

- 単一キー・複合キー両対応
- 内部ではキーを合成しマッピング

---

### 6.3 List マッピング公式ルール

1. CompareKey がある List<T> は必ずキーで揃える
2. CompareKey が無い List<T> に順序比較を行ってはならない
3. CompareKey 無し List は
   - スキップ
   - 全要素を null として並列化
   のいずれかをポリシーで選択

---

## 7. 欠損・失敗データの扱い

- 対応要素が存在しない場合は `null`
- 欠損は例外ではなく事実
- LEFT / FULL OUTER JOIN に相当

---

## 8. サンプルデータ構造（説明用）

```csharp
public sealed class Dataset
{
    public IReadOnlyList<Group> Groups { get; init; } = new List<Group>();
}

public sealed class Group
{
    public int GroupId { get; init; }
    public IReadOnlyList<Item> Items { get; init; } = new List<Item>();
}

public sealed class Item
{
    public double MetricA { get; init; }
    public IReadOnlyList<ItemDetail> Details { get; init; } = new List<ItemDetail>();
}

public sealed class ItemDetail
{
    [CompareKey]
    public int StepNo { get; init; }
    public int ValueX { get; init; }
}
```

---

## 9. ユーザーが記述するコード（最終形）

```csharp
var results =
    Compare(new[] { data0, data1 })
      .SelectMany(x => x.Groups)
      .SelectMany(g => g.Items)
      .SelectMany(i => i.Details)
      .Where(d => d[0]?.ValueX != d[1]?.ValueX)
      .Select(d => new { A = d[0]?.ValueX, B = d[1]?.ValueX })
      .ToList();
```

- join なし
- SQL なし
- 比較ロジックなし

---

## 10. DB / DataFrame との関係整理

### 10.1 類似点

- 並列次元 = 行 / バージョン
- 欠損 = null
- 内部的には OUTER JOIN に近い

### 10.2 決定的な違い

- 文字列クエリを使わない
- 型安全
- モデル側に CompareKey を宣言
- join を API から排除

本設計は **DB 的課題を型システム内で解決するクエリエンジン**である。

---

## 11. 設計思想まとめ

1. 比較 = 構造の並列化
2. すべてのメンバーは一次元増える
3. List はキーで揃える
4. 欠損は null
5. join は内部に封じ込める

---

## 12. 結論

本エンジンは

> **型安全・構造指向・並列実行モデルを備えた
> オブジェクト向けクエリエンジン**

であり、Apache Arrow / DataFrame の思想を
**C# の型システムに適合させた設計**である。

Arrow / DataFrame は内部モデルの参考にはなるが、
直接の解決策ではない。

本設計を維持することで、

- 比較対象数増加
- データ構造変更
- 欠損混在

に対しても設計は破綻しない。
