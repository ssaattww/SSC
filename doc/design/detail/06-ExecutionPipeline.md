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

出力: `TypeMetadata`

## 4. Node Construction

入力: `TypeMetadata`, `models`

処理:

- Scalar: slot 配列作成
- Object: 子ノードを再帰構築
- Container: phase4 へ委譲

出力: `Parallel<T>`

## 5. Container Normalization

優先順:

1. Dictionary
2. List/Array
3. IEnumerable（1回 materialize 後に再判定）

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
