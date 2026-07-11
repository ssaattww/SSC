namespace SSC;

public sealed class ParallelNode<T> : Parallel<T>, IParallelNode, IParallelNodeInternal
{
    private readonly T?[] _values;
    private readonly NodePresenceState[] _states;
    private readonly bool _isScalarNode;
    private readonly bool _hasRuntimeTypeMismatch;
    private readonly Dictionary<string, IReadOnlyList<IParallelNode>> _children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NodePresenceState[]> _containerPresenceStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IParallelNode> _memberNodes = new(StringComparer.Ordinal);
    private readonly List<string> _directChildOrder = [];

    /// <summary>
    /// Initializes a comparison node from aligned model values and presence states, optionally treating differing non-null runtime types as a mismatch.
    /// </summary>
    /// <param name="values">Values aligned by model index.</param>
    /// <param name="states">Presence states aligned with <paramref name="values"/>.</param>
    /// <param name="keyText">Display text for the key that identifies this node, when one exists.</param>
    /// <param name="keyValue">Raw key value used to align keyed collection elements, when one exists.</param>
    /// <param name="keyComparer">Comparer used for <paramref name="keyValue"/>, when keyed alignment requires one.</param>
    /// <param name="isScalarNode"><see langword="true"/> when this node compares values directly rather than child members.</param>
    /// <param name="detectRuntimeTypeMismatch"><see langword="true"/> to mark aligned present values with different runtime types as mismatched.</param>
    internal ParallelNode(
        T?[] values,
        NodePresenceState[] states,
        string? keyText,
        object? keyValue = null,
        IEqualityComparer<object>? keyComparer = null,
        bool isScalarNode = false,
        bool detectRuntimeTypeMismatch = false)
    {
        _values = values;
        _states = states;
        _isScalarNode = isScalarNode;
        _hasRuntimeTypeMismatch = detectRuntimeTypeMismatch && DetectRuntimeTypeMismatch(values, states);
        KeyText = keyText;
        KeyValue = keyValue;
        KeyComparer = keyComparer;
    }

    public string? KeyText { get; }

    internal object? KeyValue { get; }

    internal IEqualityComparer<object>? KeyComparer { get; }

    /// <summary>
    /// Gets whether aligned present values have differing runtime types and must be reported as a node-level mismatch without descending into members.
    /// </summary>
    internal bool HasRuntimeTypeMismatch => _hasRuntimeTypeMismatch;

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

        return new ParallelNode<T>(values.ToArray(), presenceStates, keyText, isScalarNode: true);
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

        if (_hasRuntimeTypeMismatch)
        {
            return ValueState.Mismatched;
        }

        if (!_isScalarNode)
        {
            if (HasPresenceMismatch(_states))
            {
                return ValueState.Mismatched;
            }

            foreach (var containerPresenceStates in _containerPresenceStates.Values)
            {
                if (HasPresenceMismatch(containerPresenceStates))
                {
                    return ValueState.Mismatched;
                }
            }

            if (_memberNodes.Values.Any(node => node.HasDifferences()))
            {
                return ValueState.Mismatched;
            }

            if (_children.Values.SelectMany(nodes => nodes).Any(node => node.HasDifferences()))
            {
                return ValueState.Mismatched;
            }

            return ValueState.Matched;
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

    public override string ToString()
    {
        return ParallelDisplayFormatter.FormatSlots(
            Count,
            modelIndex => new ParallelDisplaySlot(GetValue(modelIndex), GetState(modelIndex)));
    }

    public bool HasDifferences()
    {
        if (_states.Length <= 1)
        {
            return false;
        }

        if (_hasRuntimeTypeMismatch)
        {
            return true;
        }

        if (_isScalarNode)
        {
            for (var modelIndex = 0; modelIndex < _states.Length; modelIndex++)
            {
                if (GetState(modelIndex) == ValueState.Mismatched)
                {
                    return true;
                }
            }

            return false;
        }

        if (HasPresenceMismatch(_states))
        {
            return true;
        }

        foreach (var containerPresenceStates in _containerPresenceStates.Values)
        {
            if (HasPresenceMismatch(containerPresenceStates))
            {
                return true;
            }
        }

        foreach (var memberNode in _memberNodes.Values)
        {
            if (memberNode.HasDifferences())
            {
                return true;
            }
        }

        foreach (var childNodes in _children.Values)
        {
            foreach (var childNode in childNodes)
            {
                if (childNode.HasDifferences())
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IReadOnlyList<ParallelChildSet> GetDirectChildren()
    {
        if (_hasRuntimeTypeMismatch || _directChildOrder.Count == 0)
        {
            return Array.Empty<ParallelChildSet>();
        }

        var childSets = new ParallelChildSet[_directChildOrder.Count];
        for (var index = 0; index < _directChildOrder.Count; index++)
        {
            var memberName = _directChildOrder[index];
            if (_memberNodes.TryGetValue(memberName, out var memberNode))
            {
                childSets[index] = new ParallelChildSet(memberName, [memberNode], memberNode.HasDifferences());
                continue;
            }

            var childNodes = _children[memberName];
            var hasDifferences = HasPresenceMismatch(_containerPresenceStates[memberName])
                || childNodes.Any(node => node.HasDifferences());
            childSets[index] = new ParallelChildSet(memberName, childNodes, hasDifferences);
        }

        return childSets;
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

    bool IParallelNodeInternal.TryGetContainerPresenceStates(string memberName, out IReadOnlyList<NodePresenceState> states)
    {
        if (_containerPresenceStates.TryGetValue(memberName, out var containerStates))
        {
            states = containerStates;
            return true;
        }

        states = Array.Empty<NodePresenceState>();
        return false;
    }

    NodePresenceState IParallelNodeInternal.GetPresenceState(int modelIndex)
    {
        return GetPresenceState(modelIndex);
    }

    internal void SetChildren(string memberName, IReadOnlyList<IParallelNode> nodes, IReadOnlyList<NodePresenceState> presenceStates)
    {
        RegisterDirectChild(memberName);
        _children[memberName] = nodes;
        _containerPresenceStates[memberName] = [.. presenceStates];
    }

    internal void SetMemberNode(string memberName, IParallelNode node)
    {
        RegisterDirectChild(memberName);
        _memberNodes[memberName] = node;
    }

    private void RegisterDirectChild(string memberName)
    {
        if (!_directChildOrder.Contains(memberName, StringComparer.Ordinal))
        {
            _directChildOrder.Add(memberName);
        }
    }

    /// <summary>
    /// Determines whether the aligned present values contain more than one non-null runtime type.
    /// </summary>
    /// <param name="values">Values aligned by model index.</param>
    /// <param name="states">Presence states aligned with <paramref name="values"/>.</param>
    /// <returns><see langword="true"/> when at least two present non-null values have different runtime types; otherwise, <see langword="false"/>.</returns>
    private static bool DetectRuntimeTypeMismatch(
        IReadOnlyList<T?> values,
        IReadOnlyList<NodePresenceState> states)
    {
        Type? runtimeType = null;
        for (var index = 0; index < states.Count; index++)
        {
            if (states[index] != NodePresenceState.PresentValue || values[index] is null)
            {
                continue;
            }

            var currentType = values[index]!.GetType();
            if (runtimeType is null)
            {
                runtimeType = currentType;
                continue;
            }

            if (runtimeType != currentType)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPresenceMismatch(IReadOnlyList<NodePresenceState> states)
    {
        if (states.Count <= 1)
        {
            return false;
        }

        var firstState = states[0];
        for (var index = 1; index < states.Count; index++)
        {
            if (states[index] != firstState)
            {
                return true;
            }
        }

        return false;
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
