# Composite Key 説明明確化レポート

- Date: 2026-04-03
- Target: `doc/design/detail/03-ContainerRules.md`

## 修正内容

- 7.3 の文言を抽象表現から具体例中心に変更
- 以下を明示:
  - 一致条件: `(GroupId, ItemId)` の両方一致
  - 順序固定の意味: キー部品の並び順（列定義）を固定
  - 正誤例: `(GroupId, ItemId)` と `(ItemId, GroupId)` の混在禁止
  - 欠損例: 片側のみ存在する複合キーを具体化

## 目的

- 「順序差異による不一致を禁止」の意味を、実装者/利用者が同じ理解で読めるようにする
