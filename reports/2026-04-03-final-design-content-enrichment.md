# 非Draft詳細設計 内容拡充レポート

- Date: 2026-04-03
- Scope: `doc/design/detail/*.md`

## 背景

分割直後の非Draft設計は骨子中心で、ドラフトより情報密度が低かった。

## 対応

次の観点で全 9 ファイルを拡充した。

1. 契約明文化
- API 入力条件
- Strict/Non-Strict 挙動
- model slot 不変条件

2. ルール具体化
- Dictionary/List/IEnumerable の正規化規則
- keyUnion 順序と欠損規則

3. 実行手順
- フェーズ分割
- フェーズ入出力
- パイプライン擬似フロー

4. 品質要件
- 決定論
- キャッシュ
- マテリアライズ安全性
- 最低テストセット

## 行数比較（参考）

- 変更前: 合計 268 行
- 変更後: 合計 545 行

## 補足

- 実装時の正本は `doc/design/detail`。
- `doc/draft` は判断経緯の参照用。
