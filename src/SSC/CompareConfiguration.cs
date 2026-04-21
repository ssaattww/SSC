namespace SSC;

public sealed class CompareConfiguration
{
    public bool StrictMode { get; init; }

    public StringComparison StringKeyComparison { get; init; } = StringComparison.Ordinal;

    public NullKeyPolicy NullKeyPolicy { get; init; } = NullKeyPolicy.Error;

    public MissingCompareKeyListPolicy MissingCompareKeyListPolicy { get; init; } = MissingCompareKeyListPolicy.SkipAndRecordError;

    public DuplicateKeyPolicy DuplicateKeyPolicy { get; init; } = DuplicateKeyPolicy.RecordError;

    public Action<string>? TraceLog { get; init; }
}

public enum NullKeyPolicy
{
    Error,
}

public enum MissingCompareKeyListPolicy
{
    SkipAndRecordError,
}

public enum DuplicateKeyPolicy
{
    RecordError,
}
