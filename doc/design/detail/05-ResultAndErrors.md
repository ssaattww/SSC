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
- `CompareKeyNotFoundOnSequenceElement`: List 要素に CompareKey 無し
- `CompareKeyValueIsNull`: CompareKey 値 / dictionary key / sequence element が null
- `DuplicateCompareKeyDetected`: 重複キー
- `ModelIndexOutOfRange`: indexer 範囲外
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
- 文字列キーの大小文字差は入力表記を保持し、強制正規化はしない

### 5.1 Path Format

`Path` は `.` 区切りでプロパティチェーンを表現する。

例:

- `Dataset.Groups`
- `Dataset.Groups.Items`
- `Dataset.Groups.Items.MetricA`

キー関係の補助情報は `KeyText` に出し、`Path` にインデックス番号は含めない。

## 6. Recommended Error Response

- 利用者向けログ: `Code + Path + Message`
- 開発者向けログ: `ModelIndex + KeyText` も出力
