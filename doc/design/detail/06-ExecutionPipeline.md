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
- trace 有効時は property ごとのプロパティ宣言型と container category を記録

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
- dynamic value-path `GetState` 用に、比較可能な non-container member も internal member node として compare 時に事前構築できるようにする

補足:

- 上記 member node のための getter 評価は compare / node construction 中に発生し得る
- dynamic value-path `GetState` は、事前構築済み member については保存済み member state を読むだけにし、state lookup 中に getter を再実行しない
- プロパティ宣言型に無い実行時専用メンバーは、この事前構築の対象外であり、必要時は呼び出し時の反射による代替解決を使う
- generated projection の nested value path を同じ経路へ統一する作業は、この設計範囲に含めない

trace 有効時は path 単位で次を記録する。

- scalar / object / container のどの経路へ入ったか
- member getter 評価失敗時の issue 化
- child node / member node の構築結果

出力: `Parallel<T>`

## 5. Container Normalization

優先順:

1. Dictionary
2. List/Array
3. IEnumerable（1回実体化した後に再判定）

trace 有効時は次も記録する。

- プロパティ宣言型上の分類結果
- runtime type
- `IEnumerable` 実体化件数
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
