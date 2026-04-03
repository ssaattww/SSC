
# 構造並列化比較エンジン 設計詳細メモ

本ドキュメントは、直近のやり取りで議論された内容をもとに、
**内部実装と外部仕様の分離**、特に

- Compare フェーズで List をどのように消すのか
- Dictionary を使う実装が妥当かどうか

について、
**実装者がそのままコードに落とし込める粒度**で説明する。

本書は「設計判断の根拠」を残すための技術メモであり、
API 利用者向けドキュメントではない。

---

## 1. 前提となる入力モデル（正規化前）

```csharp
public sealed class Dataset
{
    public IReadOnlyList<Group> Groups { get; init; } = new List<Group>();
}

public sealed class Group
{
    [CompareKey]
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

このクラス構成が Compare の**唯一の入力**である。

---

## 2. Compare フェーズの責務

Compare フェーズの責務は次の 3 点に限定される。

1. 構造を **Parallel 化** する（一次元追加）
2. List を **CompareKey で正規化** する
3. 正規化後の構造を **再帰的に固定化** する

Compare 完了後、
- List / IReadOnlyList
- CompareKey 値
- 元の順序情報

は**一切保持されない**。

---

## 3. Parallel<Dataset> の内部実体

外部から見える型は

```csharp
Parallel<Dataset>
```

だが、内部実体は以下である。

```csharp
internal sealed class ParallelNode<T> : Parallel<T>
{
    ParallelKind Kind;                     // Scalar / Object
    T?[] Values;                          // 比較軸（dataIndex）
    Dictionary<string, IParallelNode> Children;
}
```

---

## 4. ParallelNode<Dataset> の具体状態

Compare 完了後、root ノードは次の状態になる。

```text
Kind      = Object
Values    = [ data0.Dataset, data1.Dataset ]
Children  = { "Groups" -> ParallelSequence<Group> }
```

ここで重要なのは：

- Values は「Dataset 自体」を保持している
- Dataset のプロパティ分解は **Children 側で表現**される

---

## 5. Groups プロパティで何が起きるか

### 5.1 Compare フェーズ中（一時的処理）

```text
data0.Groups = [ GroupId=1, GroupId=2 ]
data1.Groups = [ GroupId=1, GroupId=3 ]
```

ここでのみ List が存在する。

---

### 5.2 CompareKey による一時マップ生成

```text
data0Map = { 1 -> Group(1), 2 -> Group(2) }
data1Map = { 1 -> Group(1), 3 -> Group(3) }
```

このマップは**Compare フェーズ限定の一時構造**であり、
内部ノードには保存されない。

---

### 5.3 CompareKey 順序確定

```text
keyUnion = [1, 2, 3]
```

- CompareKey は「順序確定の材料」
- この時点で CompareKey の役割は終了

---

### 5.4 ParallelNode<Group> の生成

```text
ParallelNode<Group>[0]
  Values = [ Group(1), Group(1) ]

ParallelNode<Group>[1]
  Values = [ Group(2), null ]

ParallelNode<Group>[2]
  Values = [ null, Group(3) ]
```

ここに **key は存在しない**。
意味はすべて「配列の位置」に焼き込まれる。

---

### 5.5 Groups の最終保持構造

```csharp
internal sealed class ParallelSequence<Group>
{
    IReadOnlyList<Parallel<Group>> Elements;
}
```

```text
Elements[0] = ParallelNode<Group> // key=1 だった
Elements[1] = ParallelNode<Group> // key=2 だった
Elements[2] = ParallelNode<Group> // key=3 だった
```

---

## 6. Dictionary<string, IParallelNode> の位置付け

### 6.1 なぜ Dictionary を使っているか

- Compare フェーズでは型 T のプロパティを reflection で列挙する
- プロパティ名は string として取得される
- 動的に子ノードを組み立てる必要がある

この時点では

```csharp
Dictionary<string, IParallelNode>
```

が最小かつ合理的である。

---

### 6.2 Dictionary を「そのまま残す」問題点

- 型安全でない
- プロパティ名リネームに極端に弱い
- IDE 補完が効かない
- 構造指向設計が文字列解決に後退する

したがって：

> Dictionary は **構築途中専用** である

---

## 7. Dictionary を最終 API に漏らさない設計

Compare 完了後：

- Children の内容は
- 型付きアクセサ（プロパティ）として **ラップされる**

```text
parallelDataset.Groups
parallelGroup.Items
parallelItem.MetricA
```

ユーザーは Dictionary を辿らない。

---

## 8. 設計不変条件の整理

- ParallelNode<T> は **論理要素 1 件**を表す
- ParallelSequence<T> は **CompareKey 正規化結果**を表す
- CompareKey は一切保持されない
- Dictionary は Compare 構築中のみ使用される
- 外部 API から Dictionary へは到達不可

---

## 9. 結論

Dictionary を使うこと自体は **誤りではない**。

しかし：

- 構築のためにのみ使用し
- 完成形としては必ず型付き構造に変換し
- ユーザー API からは完全に隠蔽する

ことが、本設計における必須条件である。

これにより、

- 型安全
- LINQ 親和性
- 構造指向

を同時に満たす並列比較エンジンが成立する。

---

## 10. 詳細設計の粒度決定

本プロジェクトの詳細設計粒度は **L3: 実装直結粒度** とする。

- L1: 思想・方針の説明（実装不可）
- L2: 構成説明（実装者の解釈に依存）
- L3: 実装担当者が追加質問なしで着手できる（本採用）
- L4: コード等価（設計書として過剰）

L3 の完了条件:

1. 各コンポーネントの入力・出力・不変条件が明記されている
2. 分岐条件（正常/欠損/重複キー/異常系）が列挙されている
3. 公開 API シグネチャ案が確定している
4. 計算量とメモリ増分の上限見積りがある
5. テスト観点がケース表として定義されている

---

## 11. 章ごとの必要粒度（L3 基準）

### 11.1 API 境界

- `Compare(...)` のオーバーロード一覧
- `Parallel<T>` / `ParallelNode<T>` / `ParallelSequence<T>` の公開範囲
- 例外契約（投げる/投げない、例外型）
- オプション契約（List 無キー時ポリシー、重複キー時ポリシー）

### 11.2 Compare 実行パイプライン

- ステップ分解（メタデータ取得 -> List 正規化 -> ノード生成 -> 固定化）
- 各ステップの入力・出力型
- キャッシュ戦略（Reflection 情報の保持単位、スレッド安全性）
- 中間データ破棄タイミング（Dictionary と key の寿命）

### 11.3 List 正規化

- `CompareKey` 抽出規則（単一/複合、null キー扱い）
- keyUnion の順序規則（ソート順、安定性、比較器）
- 重複キー検出と処理（エラー/先勝ち/マージ禁止）
- CompareKey 無し List の既定動作

### 11.4 欠損・null セマンティクス

- 欠損と null 値の識別方針
- `AllPresent` / `AnyPresent` の定義式
- ルート欠損時の挙動

### 11.5 クエリ利用時の保証

- LINQ 連鎖時に保証される型と null 伝播
- 列挙順序の保証（再現性）
- `Count` の意味（比較対象数固定か可変か）

### 11.6 非機能

- 目標データサイズ（N 比較件数、要素数）
- 許容処理時間・メモリ上限
- ログ/診断出力方針（デバッグ時のみ、公開 API への露出なし）

### 11.7 テスト設計

- 正常系: 2 件比較、N 件比較
- 欠損系: 片側欠損、両側欠損
- 例外系: 重複キー、不正 CompareKey 設定
- 性能系: 大規模 List 正規化

---

## 12. L3 で書かないもの（過剰粒度の抑制）

- private メソッド単位の逐語的擬似コード
- 1 行レベルのアルゴリズム最適化指示
- 実装言語機能（`Span<T>` 等）の採否詳細
- 単体テストのアサート文そのもの

これらは実装・コードレビューで確定する。

---

## 13. 外部仕様の確認が必要な項目

以下は外部仕様が未確定だと実装が分岐するため、着手前に確定する。

1. CompareKey 無し List の既定動作は `Skip` と `AllNull` のどちらか
2. 重複 CompareKey 発生時は「即エラー」でよいか
3. keyUnion の順序は「昇順固定」でよいか（入力順優先が必要か）
4. 欠損と値 `null` を API 上で区別する必要があるか
5. 想定最大規模（比較対象数 N、1 List あたり件数）

---

## 14. 外部仕様の確定結果（2026-04-03）

### 14.1 CompareKey 無し List

- 挙動: `Skip`（並列化対象から除外）
- 検知時: Result にエラーを記録して返す
- オプション: strict モードでは例外送出を許可する

### 14.2 CompareKey 重複

- 挙動: エラーとして扱う
- 返却: Result にエラーを記録して返す
- オプション: strict モードでは例外送出を許可する

### 14.3 アクセスモデル（利用者視点）

利用者の期待に合わせ、**要素 index -> モデル index** の順でアクセス可能にする。

- 期待形: `Group[index][model]`
- 具体化:
  - `Groups` は `IEnumerable<Parallel<Group>>`（要素列）
  - 各 `Parallel<Group>` は `this[int modelIndex]` でモデルごとの値を返す

### 14.4 性能上限

- 事前に固定上限値は設けない
- ユーザー体感上の遅さを抑えることを優先する

### 14.5 欠損と値 null の区別

- 決定: 区別する
- 方式: `indexer` に加えて `GetState(modelIndex)` を提供し 3 値で判定する

---

## 15. 欠損と値 null の扱い（確定仕様）

`indexer` の戻り値だけでは「欠損」と「値 null」を区別できないため、以下を採用する。

1. 既存の `this[int modelIndex] : T?` は維持する
2. 追加で状態 API を提供する
   - `ValueState GetState(int modelIndex)`
3. `ValueState` は次の 3 値
   - `Missing`（要素が存在しない）
   - `PresentNull`（要素は存在し、値が null）
   - `PresentValue`（要素が存在し、値が非 null）

これにより、

- 通常利用は `indexer` だけで簡潔に書ける
- 厳密判定が必要な利用者は `GetState` を使える

---

## 16. エラーハンドリング方針（確定）

- デフォルト: 例外ではなく `Result` にエラーを集約して返す
- strict モード: 即時例外を許可する
- 例外型は詳細設計確定時に固定する（`CompareConfiguration` とセットで定義）

---

## 17. コンテナ対応方針（確定）

### 17.1 対応カテゴリ

本エンジンは、比較対象プロパティが次のいずれかである場合に正規化を行う。

1. Keyed コンテナ
   - `IDictionary<TKey,TValue>`
   - `IReadOnlyDictionary<TKey,TValue>`
2. Sequence コンテナ
   - `IList<T>` / `IReadOnlyList<T>` / `T[]`
3. 宣言型が `IEnumerable<T>` の場合
   - 実行時型を判定し、上記 1 または 2 と同等の規則を適用する

### 17.2 Keyed コンテナ（Dictionary 系）の規則

- 対応付けキーはコンテナの `TKey` をそのまま使う
- `CompareKey` 属性は不要
- keyUnion を構築し、`Parallel<TValue>` の要素列へ変換する
- 各要素は `Element[index][model]` で値参照できる

### 17.3 Sequence コンテナ（List/配列）の規則

- 要素型に `CompareKey` がある場合のみ正規化対象
- `CompareKey` 無しは `Skip + Result エラー`
- strict モードでは例外送出可

### 17.4 `IEnumerable<T>` の扱い

- `IEnumerable<T>` は受け付ける
- ただし処理開始時に一度だけマテリアライズし、以降は再列挙しない
- 実行時型が Keyed/Sequence のどちらにも該当しない場合は `Skip + Result エラー`

### 17.5 非対応コンテナ

- `IAsyncEnumerable<T>`
- ストリーム専用の one-shot 列挙体（再現不能な列挙）

これらは本フェーズでは非対応とし、入力検証で明示的にエラー化する。

### 17.6 コンテナ別の例示

#### 例1: Dictionary 系（`CompareKey` 不要）

```text
data0.Scores = { "A" -> 80, "B" -> 70 }
data1.Scores = { "A" -> 90, "C" -> 60 }
```

```text
keyUnion = ["A", "B", "C"]

Element[0] // key "A"
  Values = [80, 90]

Element[1] // key "B"
  Values = [70, null]

Element[2] // key "C"
  Values = [null, 60]
```

#### 例2: List/配列（`CompareKey` 必須）

```csharp
public sealed class ItemDetail
{
    [CompareKey]
    public int StepNo { get; init; }
    public int ValueX { get; init; }
}
```

```text
data0.Details = [{StepNo=1, ValueX=10}, {StepNo=2, ValueX=20}]
data1.Details = [{StepNo=1, ValueX=11}, {StepNo=3, ValueX=30}]
```

```text
keyUnion = [1, 2, 3]
Element[0] = [Detail(1,10), Detail(1,11)]
Element[1] = [Detail(2,20), null]
Element[2] = [null, Detail(3,30)]
```

#### 例3: `IEnumerable<T>` 宣言プロパティ

```csharp
public IEnumerable<ItemDetail> Details { get; init; } = Array.Empty<ItemDetail>();
```

- 実行時型が `List<ItemDetail>` の場合:
  - Sequence 規則で処理（1回マテリアライズ）
- 実行時型が対応外 one-shot 列挙体の場合:
  - `Skip + Result エラー`（strict で例外可）

---

## 18. LINQ `SelectMany` 動作仕様（確定）

### 18.1 基本動作

- `SelectMany` は「構造の 1 階層展開」として扱う
- モデル次元（`[model]`）を展開する演算ではない
- 展開単位は常に `Parallel<TElement>`（要素並列）である

### 18.2 順序保証

- 外側列挙順: 親コンテナの要素順
- 内側列挙順: 子コンテナの keyUnion 順
- 合成順: LINQ 標準通り「外側要素ごとに内側を順次連結」

よって、`Compare(...).SelectMany(x => x.Groups).SelectMany(g => g.Items)` は
`Group[index]` の順を保ったまま `Item[index]` を連結する。

### 18.3 欠損時の挙動

- 親要素が一部モデルで欠損でも、存在するモデルから子 keyUnion を作成する
- 全モデル欠損の親要素では、子列挙は空列となる
- 欠損判定が必要な場合は `GetState(modelIndex)` を使う

### 18.4 実装上の注意

- `SelectMany` 結果は遅延評価を維持する
- ただし `IEnumerable<T>` 入力のマテリアライズは Compare フェーズで一度だけ行う

### 18.5 `SelectMany` の例示

#### 例1: 2段階展開（Groups -> Items）

```csharp
var q =
    Compare(new[] { data0, data1 })
      .SelectMany(d => d.Groups)
      .SelectMany(g => g.Items);
```

このとき `q` は `IEnumerable<Parallel<Item>>` となり、要素順は次の規則になる。

1. `Groups` の要素順（keyUnion 順）
2. 各 Group 内の `Items` の要素順（keyUnion 順）
3. LINQ 標準どおり、外側要素ごとに内側を連結

#### 例2: 欠損を含む展開

```text
Group key=2 の Values = [Group(2), null]
Group key=3 の Values = [null, Group(3)]
```

- key=2 の `SelectMany(g => g.Items)`:
  - model0 側の Items から keyUnion を構築して展開
- key=3 の `SelectMany(g => g.Items)`:
  - model1 側の Items から keyUnion を構築して展開
- 両モデルとも欠損なら空列

#### 例3: モデル軸を展開しないことの確認

```csharp
var groups = Compare(new[] { data0, data1 }).SelectMany(d => d.Groups);
var first = groups.First();
var m0 = first[0];
var m1 = first[1];
```

`SelectMany` 後でも要素は `Parallel<Group>` のままであり、`[model]` 参照は維持される。

### 18.6 `SelectMany` の例

型の責務境界を先に固定する。

- ユーザー側で定義する型:
  - `Dataset`, `Group`, `Item`
- ライブラリ側で提供する型:
  - `Parallel<T>`, `ParallelDataset`, `ParallelGroup`, `ParallelItem`, `Api.Compare(...)`

#### 18.6.1 ユーザー側モデル（ユーザーが定義）

```csharp
using System.Collections.Generic;

namespace UserModel
{
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
        public int ItemId { get; init; }
        public double MetricA { get; init; }
    }
}
```

#### 18.6.2 ライブラリ公開型（ライブラリが提供）

```csharp
using System.Collections.Generic;
using UserModel;

namespace ParallelCompare
{
    public interface Parallel<T>
    {
        T? this[int modelIndex] { get; }
        int Count { get; }
    }

    public interface ParallelDataset : Parallel<Dataset>
    {
        IEnumerable<ParallelGroup> Groups { get; }
    }

    public interface ParallelGroup : Parallel<Group>
    {
        IEnumerable<ParallelItem> Items { get; }
    }

    public interface ParallelItem : Parallel<Item>
    {
    }

    public static class Api
    {
        public static ParallelDataset Compare(IReadOnlyList<Dataset> models) =>
            throw new System.NotImplementedException();
    }
}
```

#### 18.6.3 利用コード（ユーザーが呼び出す）

```csharp
using System.Collections.Generic;
using System.Linq;
using ParallelCompare;
using UserModel;

namespace App
{
    public static class Example
    {
        public static void Run()
        {
            Dataset data0 = new Dataset
            {
                Groups = new List<Group>
                {
                    new Group
                    {
                        GroupId = 1,
                        Items = new List<Item>
                        {
                            new Item { ItemId = 100, MetricA = 10.0 },
                            new Item { ItemId = 200, MetricA = 20.0 }
                        }
                    },
                    new Group
                    {
                        GroupId = 2,
                        Items = new List<Item>
                        {
                            new Item { ItemId = 300, MetricA = 30.0 }
                        }
                    }
                }
            };

            Dataset data1 = new Dataset
            {
                Groups = new List<Group>
                {
                    new Group
                    {
                        GroupId = 1,
                        Items = new List<Item>
                        {
                            new Item { ItemId = 100, MetricA = 11.0 },
                            new Item { ItemId = 400, MetricA = 40.0 }
                        }
                    },
                    new Group
                    {
                        GroupId = 3,
                        Items = new List<Item>
                        {
                            new Item { ItemId = 500, MetricA = 50.0 }
                        }
                    }
                }
            };

            IReadOnlyList<Dataset> models = new List<Dataset> { data0, data1 };
            ParallelDataset root = Api.Compare(models); // ここからライブラリ型

            IEnumerable<ParallelDataset> roots = new[] { root };

            // 1段目 SelectMany: ParallelDataset -> ParallelGroup
            IEnumerable<ParallelGroup> groups =
                roots.SelectMany((ParallelDataset d) => d.Groups);

            // 2段目 SelectMany: ParallelGroup -> ParallelItem
            IEnumerable<ParallelItem> items =
                groups.SelectMany((ParallelGroup g) => g.Items);

            ParallelGroup firstGroup = groups.First();
            Group? model0Group = firstGroup[0];
            Group? model1Group = firstGroup[1];

            ParallelItem firstItem = items.First();
            Item? model0Item = firstItem[0];
            Item? model1Item = firstItem[1];
        }
    }
}
```
