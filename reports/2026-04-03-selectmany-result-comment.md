# SelectMany 結果コメント追記レポート

- Date: 2026-04-03
- Target: `doc/DetailDesignDraft.md`

## 追記内容

- 章 18.6.3 の利用コードに、`SelectMany` 後の展開結果を具体コメントで追記
  - `groups` の想定内容（GroupId 単位）
  - `items` の想定内容（外側 Group 順 -> 内側 ItemId 順）
- 比較キーの想定（GroupId / ItemId）をコメントで明示

## 目的

- `SelectMany` の実行後に何が並ぶかを、コードだけで追跡できるようにする
