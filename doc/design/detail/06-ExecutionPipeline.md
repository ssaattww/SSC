# Execution Pipeline

## Phases

1. Input validation
2. Metadata resolution
3. Recursive node build
4. Container normalization
5. Result assembly

## Input Validation

- `models.Count > 0`
- `models` 要素 null 禁止

## Metadata Resolution

- `Type -> TypeMetadata` を反射で生成
- キャッシュ済みなら再利用

## Recursive Build

- Scalar: model slot 配列を直接生成
- Object: プロパティごとに子ノードを生成
- Container: `03-ContainerRules.md` を適用

## Output

- ルート: `CompareResult<T>.Root`
- 問題: `CompareResult<T>.Issues`
