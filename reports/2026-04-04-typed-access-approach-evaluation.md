# Dynamic 依存低減に向けたアクセス方式評価

- Date: 2026-04-04
- Scope: `src/SSC/ParallelDynamicAccessExtensions.cs` を中心に、補完性と性能観点で代替方式を評価

## 1. 現状整理

現在の `AsDynamic()` は次の課題を持つ。

- 補完性:
  - `dynamic` 返却のため IDE 補完がほぼ効かない
- 実行性能:
  - `DynamicObject` のバインドを経由する
  - 値パス解決ごとに `GetProperty(...)` / `GetValue(...)` を実行する
  - 値パス `GetState` が getter 実行に依存し、侵襲性とコストを持つ

## 2. 方式候補

### A. 型付きチェーン API（既存 `Children(...)` 拡張）

概要:
- `Children(...)` を基点に、型付きで値アクセスを連鎖する API を追加する
- 例: `root.Children(x => x.Groups)[0].Children(x => x.Items)[0].Value(x => x.MetricA)[0]`

実装イメージ:
- `ParallelNode<T>` 拡張として `Value<TMember>(Expression<Func<T, TMember>> selector)` を追加
- selector を `MemberExpression` に制約し、パスを静的解決
- メンバーアクセス delegate はキャッシュし、反射呼び出しを削減

長所:
- 補完が効く
- 既存 API との親和性が高く、移行しやすい
- `dynamic` バインドコストを回避できる

短所:
- `root.Groups[0]...` のような見た目にはならない
- ある程度 API 学習コストがある

### B. Source Generator による型付き Projection View 生成

概要:
- モデル型から `ParallelXxxView` を生成し、`root.Groups[0].Items[0].MetricA[0]` に近い記法を静的型で提供する

実装イメージ:
- `IIncrementalGenerator` でモデル構造を解析
- 生成クラスに `Groups`, `Items`, `MetricA` などを投影
- 内部は `ParallelNode<T>` を保持し、値取得は高速パス（キャッシュ済み accessor）を利用

長所:
- 補完性が最も高い
- 呼び出し側コードが読みやすい
- 実行時 `dynamic`/DLR 非依存

短所:
- 実装コストが最も高い（generator, テスト, デバッグ）
- 公開 API とビルドパイプラインの追加設計が必要

### C. 動的 API を残しつつ内部を事前計算ノード化（暫定）

概要:
- 外部契約を急に変えず、内部で値パス情報を比較時に事前構築しておく
- `GetState` 非侵襲化（T-042）を同時に達成する

実装イメージ:
- `BuildNode` 時にスカラー系メンバーも child node として持つ（値と状態を固定）
- 動的アクセス側はランタイム反射ではなく child node トラバースに変更
- accessor キャッシュ導入で追加コストを抑制

長所:
- 既存利用者の破壊が少ない
- 性能と侵襲性を短期で改善できる

短所:
- 補完問題は根本解決しない
- 最終的に型付き API 追加は別途必要

## 3. 推奨方針

推奨は「C -> A ->（必要なら）B」の段階移行。

1. Step 1（短期）: C
- T-042 の一環で値パス `GetState` を非侵襲化
- dynamic 実行コストの主要因（都度反射）を低減

2. Step 2（中期）: A
- `Children(...)` 系の型付き値アクセス API を正式化
- ドキュメントとサンプルを型付き API 中心へ移行
- `AsDynamic()` は互換レイヤとして残す

3. Step 3（必要時）: B
- 利用者体験をさらに改善したい場合のみ導入
- まずは PoC で保守コストを評価してから採否判断

## 4. 受け入れ基準（提案）

- 補完性:
  - サンプルコードで `dynamic` なしに主要アクセスが記述できる
- 性能:
  - 既存 dynamic 実装比で、ホットパスのアクセス時間が改善する
- 互換性:
  - 既存 `AsDynamic()` 利用コードが破壊されない
- 安全性:
  - getter 実行副作用に依存せず `GetState` が判定できる
