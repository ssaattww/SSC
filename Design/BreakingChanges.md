# Breaking Changes

## 2026-07-31

### Issue #48 `ParallelDiffPathPattern` の祖先一致

- 対象:
  - `ParallelDiffPathPattern.IsMatch(string)`
  - `ParallelDiffEntryPathExtensions.PathMatches(...)`
- 変更種別:
  - public runtime behavior の拡張
- 影響:
  - 従来はpatternと候補pathのsegment数が同じ場合だけ一致した
  - Issue #48以降はpatternの全segmentが候補pathの先頭から一致すれば、候補側の残りsegmentを子孫pathとして許容する
  - `Root.A` は `Root.A`、`Root.A.B`、`Root.A.Attribute[Width].Value` に一致する
  - segment単位で比較するため、`Root.A` は `Root.AA` と `Root.AA.B` には一致しない
- 互換性:
  - public API shape、完全一致、selector、escape、例外契約は変更しない
  - patternより浅いpathと異なるsegmentは引き続き不一致
  - 外部利用者が子孫pathを意図的に不一致として扱っていた場合、filter結果が変化する
- 背景:
  - 祖先pathを一つ指定して、その配下の子node、属性、および値の差分をまとめて無視できる必要があるため

## 2026-07-11

### T-090 polymorphic sequence の runtime 型比較

- 対象:
  - 基底型で宣言された sequence member
  - 例: `IEnumerable<Item>` に `Node` / `Content` が格納されるモデル
- 変更種別:
  - public runtime behavior の変更
- 影響:
  - 従来は sequence の宣言要素型だけで比較対象メンバーを探索していたため、基底型に比較可能なメンバーが無い場合、派生型固有メンバーの差分が `GetDiffEntries()` に現れなかった
  - T-090 以降は aligned slot の runtime 型が同一なら、そのruntime型のpublic property / fieldを再帰比較する
  - aligned slot のruntime型が異なる場合は、派生メンバーへ展開せず要素node自身を `Mismatched` として返す
  - 従来0件だった差分が新たに返るため、diff entry件数やpathを厳密に固定している利用コードは期待値更新が必要になる可能性がある
- 背景:
  - YXmlの `Node.Children : IEnumerable<Item>` では実値が `Node` / `Content` であり、`Content.Text` や `Node.Attribute` の差分が比較木構築時に欠落していたため
- 互換性:
  - public API shapeは変更しない
  - child nodeのgeneric型は引き続き宣言要素型を維持し、`GetChildren<Item>(...)` の既存アクセスを保持する
  - sequence alignment、`CompareKey`、null、Missingの既存規則は変更しない

## 2026-06-25

### T-089 generated object view `ToString()` の表示変更

- 対象:
  - Source Generator が生成する nested object view の `ToString()`
- 変更種別:
  - generated API の public convenience display 変更
- 影響:
  - 従来は generated view class の型名表示だった
  - T-089 以降は underlying `ParallelNode<T>.ToString()` に委譲し、元モデル型の `ToString()` 結果を model slot 別 value/state 形式で表示する
  - `ToString()` の文字列を厳密に比較していた利用コードは期待値更新が必要になる可能性がある
- 背景:
  - `root.Root.Attribute["id"]` のような object view でも、元モデル型が `ToString()` を実装している場合はデバッグ時にその値を直接確認できるようにするため
- 備考:
  - 機械処理では indexer / `GetState(modelIndex)` / scalar member access / Diff entry の structured data を使う
  - direct generated scalar member の `ToString()` は個別 formatter ではなく、対応する member `ParallelNode<TValue>.ToString()` に委譲する
  - dynamic projection も materialized node がある path では同じ node `ToString()` に委譲する

### T-088 `ParallelNode<T>` / generated value `ToString()` の表示変更

- 対象:
  - `ParallelNode<T>.ToString()`
  - `ParallelGeneratedValue<TModel, TValue>.ToString()`
- 変更種別:
  - public convenience display の変更
- 影響:
  - 従来は object 既定の型名表示だった
  - T-088 以降は `[0]="left"(Mismatched), [1]="right"(Mismatched)` のように model slot 別 value/state を表示する
  - `ToString()` の文字列を厳密に比較していた利用コードは期待値更新が必要になる可能性がある
- 背景:
  - デバッグ時に `Parallel` node そのものから Diff と同様に値を確認できるようにするため
- 備考:
  - 機械処理では indexer / `GetState(modelIndex)` / Diff entry の structured data を使う

### T-085 MissingCompareKeyListPolicy の既定値変更

- 対象:
  - `CompareConfiguration.MissingCompareKeyListPolicy`
  - `[CompareKey]` が無い sequence member の既定比較
- 変更種別:
  - public runtime behavior の変更
- 影響:
  - 従来は `CompareKey` が無い sequence member で `CompareKeyNotFoundOnSequenceElement` を Error 記録し、配下 node をスキップしていた
  - T-085 以降の既定値は `AlignByIndex` となり、同条件では ordinal index で要素を揃えて比較する
  - 旧挙動が必要な利用コードは `MissingCompareKeyListPolicy.SkipAndRecordError` を明示する必要がある
- 背景:
  - gist `XmlCustom.cs` の `Node.Children` / `Node.ChildrenOfNode` は `[CompareKey]` なしの `IEnumerable<T>` であり、既定 `Compare(...)` で parsed `Document` 比較を成功させる必要があったため
- 備考:
  - `CompareKey` が存在する sequence では従来どおり key union で比較する

### T-084 Source Generator object member の生成型変更

- 対象:
  - class / struct 型の direct object member に対する generated projection
  - 例: `Document.Root`
- 変更種別:
  - generated API の source shape 変更
- 影響:
  - 従来は `Document.Root` 相当の member が `ParallelGeneratedValue<TModel, TMember>` として生成されていた
  - T-084 以降は nested generated view として生成され、`root.Root.Name` / `root.Root.Attribute` / `root.Root.Range` のように配下 member を直接辿れる
  - 旧 generated 型へ明示代入していた利用コードは source 互換でない可能性がある
- 背景:
  - gist `YXml.cs` の `Document.Root` 配下 generated member が不完全になり、Source Generator の型付き導線で class model 配下を辿れなかったため
- 備考:
  - 保守性と性能を優先し、object member は既存比較ツリーの member-node dictionary から直接 nested view 化する

## 2026-04-21

### T-070 `IParallelNode` への探索 API 追加

- 対象:
  - `IParallelNode.HasDifferences()`
  - `IParallelNode.GetDirectChildren()`
  - `ParallelChildSet`
  - `ParallelChildSet.HasDifferences`
- 変更種別:
  - public interface へのメンバー追加
- 影響:
  - `IParallelNode` を独自実装している利用者は、新メンバー実装が必要になる
  - 既存 binary / source 互換性を維持できない可能性がある
- 背景:
  - 差分のある直下子要素をライブラリ外から探索できる共通面を公開するため
- 備考:
  - T-070 は設計反映段階で breaking change として記録した
