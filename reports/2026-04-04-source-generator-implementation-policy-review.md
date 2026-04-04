# Source Generator 実装方針レビュー（事前）

- Date: 2026-04-04
- Context: dynamic 互換を維持しつつ、案3（Source Generator）を直接実装する

## 1. 決定した実装方針

1. 新規プロジェクト `src/SSC.Generators` を追加し、`IIncrementalGenerator` で型付き view を生成する
2. 生成対象は `[GenerateParallelView]` 付き型に限定する
3. 生成 API は `AsGeneratedView(this Parallel<T>)` を root 入口にする
4. runtime 側に生成コード共通基盤（list/value wrapper, `NodeMeta`）を追加する
5. 既存 `AsDynamic()` は変更せず互換維持する
6. 配布は `ssaattww.SSC`（runtime）と `ssaattww.SSC.Generators`（analyzer）の分離構成で固定する

## 2. 生成コードの公開契約

- root:
  - `result.Root!.AsGeneratedView()`
- container:
  - `root.Groups[0].Items[0]`
- scalar value:
  - `root.Groups[0].Items[0].MetricA[0]`
  - `root.Groups[0].Items[0].MetricA.GetState(1)`
- node metadata:
  - `NodeCount`, `NodeKeyText`, `GetState(modelIndex)`

## 3. 事前レビュー（実装前チェック）

### High

- なし

### Medium

- 生成対象の判定が広すぎると意図しない型でコードが増える
  - 対策: 属性明示型のみ生成
- nested type 名の衝突/無効識別子化リスク
  - 対策: fully-qualified symbol 名からサニタイズした view 名を採用
- 既存 dynamic API 回帰
  - 対策: 既存 dynamic E2E を変更せず回帰確認

### Low

- 初期版は complex value の深いプロパティ連鎖を `Detail[modelIndex]?.Label` 形式で扱う場面が残る
  - 対策: まず root/container/scalar 主導線を安定化し、拡張は追跡課題化

## 4. 実行可否判定

- 判定: 実行可
- 理由:
  - 公開契約を壊さず段階的導入できる
  - 補完性は generated API で改善し、dynamic 依存を主導線から外せる
