namespace SSC;

public sealed class ParallelNode<T> : Parallel<T>, IParallelNode, IParallelNodeInternal
{
    private readonly T?[] _values;
    private readonly NodePresenceState[] _states;
    private readonly Dictionary<string, IReadOnlyList<IParallelNode>> _children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IParallelNode> _memberNodes = new(StringComparer.Ordinal);

    internal ParallelNode(T?[] values, NodePresenceState[] states, string? keyText)
    {
        _values = values;
        _states = states;
        KeyText = keyText;
    }

    public string? KeyText { get; }

    public int Count => _values.Length;

    public bool AllPresent => _states.All(state => state == NodePresenceState.PresentValue);

    public bool AnyPresent => _states.Any(state => state != NodePresenceState.Missing);

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
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(states);
        if (values.Count != states.Count)
        {
            throw new ArgumentException(
                $"values count '{values.Count}' must match states count '{states.Count}'.",
                nameof(states));
        }

        var presenceStates = new NodePresenceState[states.Count];
        for (var index = 0; index < states.Count; index++)
        {
            presenceStates[index] = states[index] == ValueState.Missing
                ? NodePresenceState.Missing
                : values[index] is null
                    ? NodePresenceState.PresentNull
                    : NodePresenceState.PresentValue;
        }

        return new ParallelNode<T>(values.ToArray(), presenceStates, keyText);
    }

    public ValueState GetState(int modelIndex)
    {
        ValidateIndex(modelIndex);
        var baseState = _states[modelIndex];
        if (baseState == NodePresenceState.Missing)
        {
            return ValueState.Missing;
        }

        if (_states.Length <= 1)
        {
            return ValueState.Missing;
        }

        var matched = true;
        for (var index = 0; index < _states.Length; index++)
        {
            if (index == modelIndex)
            {
                continue;
            }

            if (_states[index] == NodePresenceState.Missing)
            {
                matched = false;
                break;
            }

            if (_states[index] != baseState)
            {
                matched = false;
                break;
            }

            if (baseState == NodePresenceState.PresentValue
                && !EqualityComparer<T?>.Default.Equals(_values[modelIndex], _values[index]))
            {
                matched = false;
                break;
            }
        }

        return ValueStateExtensions.ToComparisonState(hasComparisonTarget: true, matched);
    }

    public object? GetValue(int modelIndex)
    {
        ValidateIndex(modelIndex);
        return _states[modelIndex] == NodePresenceState.PresentValue ? _values[modelIndex] : null;
    }

    internal NodePresenceState GetPresenceState(int modelIndex)
    {
        ValidateIndex(modelIndex);
        return _states[modelIndex];
    }

    public IReadOnlyList<ParallelNode<TElement>> GetChildren<TElement>(string memberName)
    {
        if (!_children.TryGetValue(memberName, out var nodes))
        {
            return Array.Empty<ParallelNode<TElement>>();
        }

        return nodes.Select(node => (ParallelNode<TElement>)node).ToArray();
    }

    Type IParallelNodeInternal.ModelType => typeof(T);

    bool IParallelNodeInternal.TryGetChildren(string memberName, out IReadOnlyList<IParallelNode> nodes)
    {
        return _children.TryGetValue(memberName, out nodes!);
    }

    bool IParallelNodeInternal.TryGetMemberNode(string memberName, out IParallelNode node)
    {
        return _memberNodes.TryGetValue(memberName, out node!);
    }

    NodePresenceState IParallelNodeInternal.GetPresenceState(int modelIndex)
    {
        return GetPresenceState(modelIndex);
    }

    internal void SetChildren(string memberName, IReadOnlyList<IParallelNode> nodes)
    {
        _children[memberName] = nodes;
    }

    internal void SetMemberNode(string memberName, IParallelNode node)
    {
        _memberNodes[memberName] = node;
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
