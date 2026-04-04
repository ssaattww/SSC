# Source Generator 方針ロードマップ

- Date: 2026-04-04
- Goal: `dynamic` 依存を段階的に解消し、最終的に生成型 API で補完性と実行性能を改善する

## 1. 採用方針

- 最終到達点は「案3: Source Generator」
- ただし一気に置換せず、破壊的変更を避けるため段階導入する

## 2. 実装計画

### Phase A: 基盤整備（T-042 と整合）

- 値パス `GetState` を非侵襲化し、getter 実行依存を除去
- 値アクセスの反射パスをキャッシュ可能な構造へ整理
- 目的:
  - 生成 API 導入前でも性能/安全性を底上げ
  - 生成コードが呼ぶ内部 API を安定化

### Phase B: Generator PoC

- 新規プロジェクト（例: `src/SSC.Generators`）を追加
- `IIncrementalGenerator` で Compare 対象モデルのメンバーを解析
- 生成物 PoC:
  - `ParallelDatasetView` 相当の typed view
  - child container と scalar 値アクセス
  - `GetState(modelIndex)` 連携

完了条件:
- サンプルで `dynamic` なしに階層アクセスが成立
- 既存 API と併用できる

### Phase C: 公開 API 化

- 拡張メソッド追加（例: `AsGeneratedView()`）
- `AsDynamic()` は互換用として残す
- 設計書と README の主導線を generated API へ切替

完了条件:
- ドキュメント例が generated API 中心
- E2E/Unit で generated API 経路をカバー

## 3. パッケージ構成（決定）

### generator を別 NuGet として分離

- `ssaattww.SSC`（runtime）と `ssaattww.SSC.Generators`（analyzer）を分離
- 利点:
  - 配布責務が明確
  - generator 依存更新を独立運用しやすい

## 4. リスクと対策

- リスク: 生成対象の型制約が曖昧だと生成失敗が増える
  - 対策: 初期は「直接メンバー参照のみ」など生成規則を狭く定義
- リスク: 既存 API との二重運用で仕様差が出る
  - 対策: 共通内部 API を介して挙動を一元化
- リスク: build 時間増加
  - 対策: incremental generator を採用し、再生成を最小化

## 5. 直近アクション

1. T-042（非侵襲化）を先行実施
2. `src/SSC.Generators` の PoC スケルトンを追加
3. generated API の最小 E2E テストを追加
4. `ssaattww.SSC.Generators` として配布可能な pack 設定を整備
