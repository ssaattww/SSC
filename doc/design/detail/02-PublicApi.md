# Public API

## 1. Entry Point

```csharp
public static class ParallelCompareApi
{
    public static CompareResult<T> Compare<T>(
        IReadOnlyList<T> models,
        CompareConfiguration? configuration = null);
}
```

## 2. Input Contract

- `models.Count == 0` は `InputModelListEmpty`
- `models` 要素 `null` は `InputModelNullElement`
- `configuration == null` は既定値を適用

## 3. Parallel Node API

```csharp
public interface Parallel<T>
{
    T? this[int modelIndex] { get; }
    int Count { get; }
    bool AllPresent { get; }
    bool AnyPresent { get; }
    ValueState GetState(int modelIndex);
}
```

```csharp
public interface ParallelDataset : Parallel<Dataset>
{
    IEnumerable<ParallelGroup> Groups { get; }
}

public interface ParallelGroup : Parallel<Group>
{
    IEnumerable<ParallelItem> Items { get; }
}

public interface ParallelItem : Parallel<Item>
{
}
```

## 4. Behavior Contract

- indexer の範囲外アクセスは `ModelIndexOutOfRange`
- `AllPresent == Values.All(v => v != null かつ Missing でない)`
- `AnyPresent == Values.Any(v => Missing でない)`

## 5. Configuration Entry

```csharp
public sealed class CompareConfiguration
{
    public bool StrictMode { get; init; } = false;
    public StringComparison StringKeyComparison { get; init; } = StringComparison.Ordinal;
    public NullKeyPolicy NullKeyPolicy { get; init; } = NullKeyPolicy.Error;
    public MissingCompareKeyListPolicy MissingCompareKeyListPolicy { get; init; } =
        MissingCompareKeyListPolicy.SkipAndRecordError;
    public DuplicateKeyPolicy DuplicateKeyPolicy { get; init; } =
        DuplicateKeyPolicy.RecordError;
}
```

## 6. Result Entry

`Compare` は常に `CompareResult<T>` を返し、成功時は `Root` が設定される。
strict 時は Error 発生で例外送出を許可する。

## 7. Exception Types (Strict Mode)

例外は 2 系統に分ける。

```csharp
public class CompareInputException : Exception
{
    public CompareIssueCode Code { get; }
    public CompareInputException(CompareIssueCode code, string message) : base(message) => Code = code;
}

public class CompareExecutionException : Exception
{
    public CompareIssueCode Code { get; }
    public CompareExecutionException(CompareIssueCode code, string message) : base(message) => Code = code;
}
```

- `CompareInputException`:
  - 入力妥当性違反（空 model、null 要素など）
- `CompareExecutionException`:
  - 正規化・反射・キー処理など実行中エラー
