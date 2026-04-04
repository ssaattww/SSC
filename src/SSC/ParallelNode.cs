namespace SSC;

public sealed class ParallelNode<T> : Parallel<T>, IParallelNode
{
    private readonly T?[] _values;
    private readonly ValueState[] _states;
    private readonly Dictionary<string, IReadOnlyList<IParallelNode>> _children = new(StringComparer.Ordinal);

    internal ParallelNode(T?[] values, ValueState[] states, string? keyText)
    {
        _values = values;
        _states = states;
        KeyText = keyText;
    }

    public string? KeyText { get; }

    public int Count => _values.Length;

    public bool AllPresent => _states.All(state => state == ValueState.PresentValue);

    public bool AnyPresent => _states.Any(state => state != ValueState.Missing);

    public T? this[int modelIndex]
    {
        get
        {
            ValidateIndex(modelIndex);
            return _values[modelIndex];
        }
    }

    public static ParallelNode<T> CreateLeaf(IReadOnlyList<T?> values, IReadOnlyList<ValueState> states, string? keyText = null)
    {
        return new ParallelNode<T>(values.ToArray(), states.ToArray(), keyText);
    }

    public ValueState GetState(int modelIndex)
    {
        ValidateIndex(modelIndex);
        return _states[modelIndex];
    }

    public object? GetValue(int modelIndex)
    {
        return this[modelIndex];
    }

    public IReadOnlyList<ParallelNode<TElement>> GetChildren<TElement>(string memberName)
    {
        if (!_children.TryGetValue(memberName, out var nodes))
        {
            return Array.Empty<ParallelNode<TElement>>();
        }

        return nodes.Select(node => (ParallelNode<TElement>)node).ToArray();
    }

    internal void SetChildren(string memberName, IReadOnlyList<IParallelNode> nodes)
    {
        _children[memberName] = nodes;
    }

    private void ValidateIndex(int modelIndex)
    {
        if (modelIndex >= 0 && modelIndex < _values.Length)
        {
            return;
        }

        throw new CompareExecutionException(
            CompareIssueCode.ModelIndexOutOfRange,
            $"modelIndex '{modelIndex}' is out of range for count '{_values.Length}'.");
    }
}
