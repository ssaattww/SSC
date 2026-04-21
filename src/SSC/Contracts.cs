namespace SSC;

public enum ValueState
{
    Missing,
    Matched,
    Mismatched,
}

public enum CompareIssueLevel
{
    Error,
    Warning,
}

public enum CompareIssueCode
{
    InputModelListEmpty,
    InputModelNullElement,
    UnsupportedContainerType,
    CompareKeyNotFoundOnSequenceElement,
    CompareKeyValueIsNull,
    DuplicateCompareKeyDetected,
    ModelIndexOutOfRange,
    ReflectionMetadataBuildFailed,
}

public sealed class CompareIssue
{
    public CompareIssueLevel Level { get; init; }

    public CompareIssueCode Code { get; init; }

    public string Path { get; init; } = string.Empty;

    public int? ModelIndex { get; init; }

    public string? KeyText { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed class CompareResult<T>
{
    public Parallel<T>? Root { get; init; }

    public IReadOnlyList<CompareIssue> Issues { get; init; } = Array.Empty<CompareIssue>();

    public bool HasError { get; init; }
}

public interface Parallel<T>
{
    T? this[int modelIndex] { get; }

    int Count { get; }

    bool AllPresent { get; }

    bool AnyPresent { get; }

    ValueState GetState(int modelIndex);
}

public interface IParallelNode
{
    int Count { get; }

    bool AllPresent { get; }

    bool AnyPresent { get; }

    string? KeyText { get; }

    object? GetValue(int modelIndex);

    ValueState GetState(int modelIndex);

    bool HasDifferences();

    IReadOnlyList<ParallelChildSet> GetDirectChildren();
}

public readonly struct ParallelChildSet
{
    public ParallelChildSet(string name, IReadOnlyList<IParallelNode> nodes, bool hasDifferences)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(nodes);
        Name = name;
        Nodes = nodes;
        HasDifferences = hasDifferences;
    }

    public string Name { get; }

    public IReadOnlyList<IParallelNode> Nodes { get; }

    public bool HasDifferences { get; }
}

internal interface IParallelNodeInternal
{
    Type ModelType { get; }

    bool TryGetChildren(string memberName, out IReadOnlyList<IParallelNode> nodes);

    bool TryGetMemberNode(string memberName, out IParallelNode node);

    NodePresenceState GetPresenceState(int modelIndex);
}
