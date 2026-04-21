# Execution Pipeline

## 1. Phase Overview

1. Input Validation
2. Metadata Resolution
3. Node Construction
4. Container Normalization
5. Result Assembly

## 2. Input Validation

入力: `IReadOnlyList<T> models`

検証:

- 空配列禁止
- null 要素禁止
- configuration の既定値補完

出力:

- 正常: phase2 へ
- 異常: Issue 記録（strict なら例外）

## 3. Metadata Resolution

入力: `Type rootType`

処理:

- public プロパティ列挙
- container 種別判定
- CompareKey 抽出ルール構築
- `CompareIgnore` 付与メンバーを除外
- trace 有効時は property ごとの declared type と container category を記録

反射対象ポリシー:

- 基本は public getter メンバーを広く対象とする
- 比較から外したいメンバーだけ `CompareIgnore` で明示除外する
- 制限は最小化し、除外指定で制御する

出力: `TypeMetadata`

## 4. Node Construction

入力: `TypeMetadata`, `models`

処理:

- Scalar: slot 配列作成
- Object: 子ノードを再帰構築
- Container: phase4 へ委譲

trace 有効時は path 単位で次を記録する。

- scalar / object / container のどの経路へ入ったか
- member getter 評価失敗時の issue 化
- child node / member node の構築結果

出力: `Parallel<T>`

## 5. Container Normalization

優先順:

1. Dictionary
2. List/Array
3. IEnumerable（1回 materialize 後に再判定）

trace 有効時は次も記録する。

- declared type 上の分類結果
- runtime type
- `IEnumerable` materialize 件数
- compare key 解決結果
- issue 記録や skip 判定

出力: `IEnumerable<Parallel<TElement>>`

## 6. Result Assembly

- Root 設定
- Issues 集約
- `HasError = Issues.Any(Level==Error)`

## 7. Pseudo Flow

```text
Validate -> ResolveMetadata -> BuildNodeRecursively
       -> NormalizeContainers -> BuildResult
```
