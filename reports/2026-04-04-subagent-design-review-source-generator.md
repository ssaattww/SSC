# Subagent 設計レビュー: Source Generator 導入

- Date: 2026-04-04
- Reviewer: subagent (`019d5784-c242-7270-9892-5ccfff372d5d`)
- Scope:
  - `doc/design/detail/01-DomainModel.md`
  - `doc/design/detail/02-PublicApi.md`
  - `doc/design/detail/08-ImplementationChecklist.md`
  - `reports/2026-04-04-source-generator-implementation-policy-review.md`

## 1. 主要指摘（抜粋）

- High:
  - 配布戦略（別 NuGet / 同梱）が未確定
- Medium:
  - generated API 初期スコープが曖昧
  - メタ情報命名衝突規則が未定義
  - 命名/配置の一意化ルール不足
  - `AsGeneratedView()` の失敗契約が未定義
  - Dictionary 表現の契約不足
- Low:
  - generator 固有コンパイル検証項目の不足
  - dynamic 回帰観点の明示不足

## 2. 反映内容

- 配布戦略を「runtime/generator 分離」に固定
  - `reports/2026-04-04-source-generator-roadmap.md`
- generated API の初期スコープを明文化
  - container/value/nested `Select` / out-of-scope を定義
- node メタ情報を `NodeMeta` 配下へ統一
- `AsGeneratedView()` の非 compare node 失敗契約（`ArgumentException`）を明記
- Dictionary を key union index アクセスで扱う契約を明記
- checklist に generator コンパイル検証・dynamic 回帰観点を追加

## 3. 判定

- 指摘は設計と実装へ反映済み
- 実装着手可と判断
