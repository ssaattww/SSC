# Runtime Code Generation Options

- Date: 2026-04-04
- Purpose: Source Generator（build-time）とは別に、runtime でコード生成/実行する方式を整理する

## 1. Reflection.Emit / Expression Compile

概要:
- runtime で動的型・メソッドを生成する（IL 生成 or 式木をコンパイル）

長所:
- ビルド不要で即時に実行可能
- 特化パスを生成すれば高速化余地がある

短所:
- 実装難易度が高い（IL/型生成の保守コスト）
- AOT/環境差の制約を受けやすい
- デバッグ性が低い

## 2. Roslyn Compilation API（runtime compile）

概要:
- `Microsoft.CodeAnalysis.CSharp` で C# ソースを runtime コンパイルし、DLL をロードして実行する

長所:
- C# ソースとして生成でき、IL 直書きより可読性が高い
- 動的に型付き API を作る設計が可能

短所:
- 依存管理・セキュリティ・ロード戦略（AssemblyLoadContext）が重い
- 初回コンパイルコストが大きい
- デプロイ構成が複雑化する

## 3. Pre-generated Code の切替読み込み

概要:
- 事前生成した複数実装を同梱し、runtime では条件に応じて切替利用する

長所:
- runtime でのコンパイル不要
- 予測しやすい運用（署名/監査/再現性）

短所:
- 柔軟性が低い（想定外形状への追従が難しい）
- 生成資産と切替ロジックの管理コストが増える

## Roslyn Scripting との関係

- `Microsoft.CodeAnalysis.CSharp.Scripting` は「スクリプト実行」用途で、通常の型生成/配布前提の API とは別系統。
- runtime で型付き API を生成して利用する主軸は、通常 `CSharpCompilation`（Compilation API）を使う設計になる。
- したがって、今回の「runtime で生成するならどれか」という文脈では、Scripting は補助的選択肢であり主方式ではない。

## 補足（今回実装との関係）

- 現在の実装は Source Generator であり、build-time でのみコード生成される。
- runtime 生成は採用していない。
