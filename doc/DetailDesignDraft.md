
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
