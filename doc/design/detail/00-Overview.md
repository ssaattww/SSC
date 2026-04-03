# Detailed Design Overview

## 1. Purpose

本設計は、複数モデルを「同一構造位置」で横並びにし、
利用者が LINQ だけで比較・抽出できる比較エンジンを実現するための詳細設計である。

## 2. Design Scope

本設計に含む:

- 構造並列化（Lifting）
- Container 正規化（Dictionary / List / IEnumerable）
- 欠損・null セマンティクス
- Public API / Result / Error 契約
- LINQ（特に SelectMany）の動作保証

本設計に含まない:

- UI/表示層
- ストレージ永続化
- 分散実行

## 3. Core Principles

1. 比較は差分計算 API ではなく、構造の並列化で表現する
2. 利用者は `element[index][model]` で model ごとの値へ到達できる
3. 欠損は例外でなくデータ状態として保持する
4. join 相当処理は内部に隠蔽し、利用者へ露出しない

## 4. Deterministic Contract

- 同一入力に対し、同一列挙順を保証する
- key 連結順は決定論的である
- SelectMany 後も `ParallelXxx` 形式を維持する

## 5. Read Order

1. `01-DomainModel.md`
2. `02-PublicApi.md`
3. `03-ContainerRules.md`
4. `04-SelectManySemantics.md`
5. `05-ResultAndErrors.md`
6. `06-ExecutionPipeline.md`
7. `07-NonFunctional.md`
8. `08-ImplementationChecklist.md`

## 6. Relationship With Draft

- `doc/draft/DetailDesignDraft.md` は判断経緯と議論ログ
- `doc/design/detail/*` は実装時の正本
