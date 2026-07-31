# Issue #48 TDD 赤確認準備

## 対象

`ParallelDiffPathPattern` に祖先 path を指定した場合、その配下の子孫差分まで一致対象とする契約を追加する。

## 追加した再現テスト

- pattern 自身への完全一致
- 子 node、属性、値を含む子孫 path への一致
- `Root.A` と `Root.AA` を区別する path segment 境界
- wildcard selector を含む祖先 pattern
- LINQ filter による子孫差分の一括除外

## 赤確認方法

このreport作成時点のHEADではproduction codeを変更していない。既存実装はpatternと候補pathのsegment数が異なる場合に不一致を返すため、子孫一致を要求する新規テストは失敗する見込みである。

PR作成後、このHEAD SHAに紐づくGitHub Actions runで実際の失敗を確認し、run ID、失敗テスト、artifact内容を追記する。
