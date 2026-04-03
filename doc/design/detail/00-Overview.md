# Detailed Design Overview

## Scope

本設計は、複数モデルを構造位置ごとに並列化し、LINQ で比較可能にする比較エンジンを対象とする。

## Core Principles

1. 比較は差分抽出ではなく構造の並列化（Lifting）
2. 要素アクセスは `element[index][model]` で統一
3. 欠損は例外でなくデータ状態として扱う
4. join は内部に閉じ込める

## Document Map

- データモデル: `01-DomainModel.md`
- 公開 API: `02-PublicApi.md`
- コンテナ規則: `03-ContainerRules.md`
- LINQ/SelectMany: `04-SelectManySemantics.md`
- Result/エラー: `05-ResultAndErrors.md`
- 実行手順: `06-ExecutionPipeline.md`
- 非機能: `07-NonFunctional.md`
- 実装前確認: `08-ImplementationChecklist.md`
