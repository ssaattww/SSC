# Result And Errors

## Result Shape

```csharp
public sealed class CompareResult<T>
{
    public Parallel<T>? Root { get; init; }
    public IReadOnlyList<CompareIssue> Issues { get; init; } = Array.Empty<CompareIssue>();
    public bool HasError { get; init; }
}
```

## Issue Shape

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
```

## Issue Codes

- `InputModelListEmpty`
- `InputModelNullElement`
- `UnsupportedContainerType`
- `CompareKeyNotFoundOnSequenceElement`
- `CompareKeyValueIsNull`
- `DuplicateCompareKeyDetected`
- `ModelIndexOutOfRange`
- `ReflectionMetadataBuildFailed`

## Strict Mode

- `StrictMode=false`: `Issues` へ蓄積して返却
- `StrictMode=true`: Error 発生時点で例外
