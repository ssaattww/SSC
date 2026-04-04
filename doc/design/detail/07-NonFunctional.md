# Non-Functional Design

## 1. Determinism

- 同一入力で同一列挙順
- keyUnion 順序は決定論的
- 文字列キー比較は `CompareConfiguration.StringKeyComparison` に従う（既定: `Ordinal`）
- `OrdinalIgnoreCase` 時の `KeyText` は Ordinal 最小表記へ正規化する

## 2. Reflection Metadata Cache

- 構造: `ConcurrentDictionary<Type, TypeMetadata>`
- 生成後は不変オブジェクトとして扱う
- 多スレッド読み取りを許可

## 3. Enumerable Safety

- `IEnumerable<T>` は Compare 開始時に 1 回だけ `List<T>` 化
- 比較中は再列挙禁止
- one-shot 列挙は明示エラー

## 4. Time Complexity (Reference)

- Dictionary 正規化: `O(U + M)`
  - `U`: union key 数
  - `M`: 全 model の要素総数
- List 正規化: `O(U + M)`（CompareKey 抽出を含む）

## 5. Memory Policy

- 中間マップは正規化完了後に破棄
- Root と公開ノードのみ長寿命
- 大規模入力では keyUnion サイズを監視

## 6. Logging / Diagnostics

- 既定では最小ログ（Issue のみ）
- デバッグ時に phase 境界ログを有効化可能

## 7. Scalability Assumption

- 事前固定上限は置かない
- 体感遅延を優先し、必要時にキャッシュ・分割最適化を実施

## 8. Performance Acceptance

- 本フェーズの受け入れ基準は「正しく動作すること」を優先する
- 性能閾値は固定しない
- 性能課題は実データ観測後に別タスクで改善する
