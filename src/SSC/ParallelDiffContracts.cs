using System.Globalization;

namespace SSC;

public enum ParallelDiffEntryKind
{
    Node,
    ContainerPresence,
}

public sealed class ParallelDiffEntry
{
    public string Path { get; init; } = string.Empty;

    public string? ParentPath { get; init; }

    public ParallelDiffEntryKind Kind { get; init; }

    public IParallelNode? ParentNode { get; init; }

    public IParallelNode? Node { get; init; }

    public IReadOnlyList<ParallelDiffValue> Values { get; init; } = Array.Empty<ParallelDiffValue>();

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Path}: {string.Join(", ", Values.Select(value => value.ToString()))}");
    }
}

public sealed class ParallelDiffValue
{
    public int ModelIndex { get; init; }

    public object? Value { get; init; }

    public ValueState State { get; init; }

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{ModelIndex}]={FormatValue()}({State})");
    }

    private string FormatValue()
    {
        if (State == ValueState.Missing)
        {
            return "<missing>";
        }

        if (Value is null)
        {
            return "null";
        }

        if (Value is string text)
        {
            return string.Create(CultureInfo.InvariantCulture, $"\"{text}\"");
        }

        return Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
