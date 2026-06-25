# Result And Errors

## 1. Result Model

```csharp
public sealed class CompareResult<T>
{
    public Parallel<T>? Root { get; init; }
    public IReadOnlyList<CompareIssue> Issues { get; init; } = Array.Empty<CompareIssue>();
    public bool HasError { get; init; }
}
```

## 2. Issue Model

```csharp
public sealed class CompareIssue
{
    public CompareIssueLevel Level { get; init; }
    public CompareIssueCode Code { get; init; }
    public string Path { get; init; } = "";
    public int? ModelIndex { get; init; }
    public string? KeyText { get; init; }
    public string Message { get; init; } = "";
}

public enum CompareIssueLevel { Error, Warning }
```

## 3. Issue Codes And Triggers

- `InputModelListEmpty`: models が空
- `InputModelNullElement`: models 内に null
- `UnsupportedContainerType`: 未対応コンテナ
- `CompareKeyNotFoundOnSequenceElement`: `MissingCompareKeyListPolicy.SkipAndRecordError` 指定時に List 要素に CompareKey 無し
- `CompareKeyValueIsNull`: CompareKey 値 / dictionary key / sequence element が null
- `DuplicateCompareKeyDetected`: 重複キー
- `ModelIndexOutOfRange`: indexer 範囲外
- `KeyNotFound`: key text indexer の key 未検出
- `ReflectionMetadataBuildFailed`: 反射メタデータ構築失敗

## 4. Strict Mode Matrix

- Strict=false:
  - Error を Issues に蓄積
  - 継続可能な範囲で比較継続
- Strict=true:
  - Error 時点で例外
  - `Root` は未完成状態として扱う

例外種別:

- 入力系 Error: `CompareInputException`
- 実行系 Error: `CompareExecutionException`

## 5. Required Issue Fields

- `Path`: 例 `Dataset.Groups.Items`
- `ModelIndex`: 特定可能時に設定
- `KeyText`: キー問題時に設定
- `Message`: 人間が読める説明

`KeyText` の運用:

- null 系エラー（null key / null sequence element）は `"<null>"` を設定
- `OrdinalIgnoreCase` 時の文字列キーは同値候補の `StringComparer.Ordinal` 最小表記へ正規化する

### 5.1 Path Format

`CompareIssue.Path` は `.` 区切りでプロパティチェーンを表現する。

例:

- `Dataset.Groups`
- `Dataset.Groups.Items`
- `Dataset.Groups.Items.MetricA`

キー関係の補助情報は `KeyText` に出し、`Path` にインデックス番号は含めない。

`CompareIssue.Path` は診断対象のプロパティ位置を示すための簡易 path であり、
差分表示 helper が返す XPath-like path とは別契約である。

- `CompareIssue.Path`:
  - issue の発生位置を表す
  - container の key / ordinal discriminator は含めない
  - key 関係の補助情報は `KeyText` で表す
- XPath-like path:
  - 比較結果 tree 内の node を一意に辿るために使う
  - container segment に `Items[100]` / `Items[#0]` のような discriminator を含める
  - 詳細 grammar は `02-PublicApi.md` の XPath-like path access 契約で定義する

## 6. Recommended Error Response

- 利用者向けログ: `Code + Path + Message`
- 開発者向けログ: `ModelIndex + KeyText` も出力
