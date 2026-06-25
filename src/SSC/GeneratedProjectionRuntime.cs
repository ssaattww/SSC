namespace SSC;

public readonly struct ParallelGeneratedMeta
{
    private readonly IParallelNode _node;

    public ParallelGeneratedMeta(IParallelNode node)
    {
        _node = node;
    }

    public int Count => _node.Count;

    public string? KeyText => _node.KeyText;

    public ValueState GetState(int modelIndex) => _node.GetState(modelIndex);
}

public sealed class ParallelGeneratedDictionary<TKey, TElement, TView> : IEnumerable<TView>
    where TKey : notnull
{
    private readonly IReadOnlyList<ParallelNode<TElement>> _nodes;
    private readonly int _modelCount;
    private readonly Func<ParallelNode<TElement>, TView> _viewFactory;
    private Dictionary<string, int>? _keyIndexCache;
    private Dictionary<object, int>? _keyValueIndexCache;

    public ParallelGeneratedDictionary(
        IReadOnlyList<ParallelNode<TElement>> nodes,
        int modelCount,
        Func<ParallelNode<TElement>, TView> viewFactory)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentOutOfRangeException.ThrowIfNegative(modelCount);
        ArgumentNullException.ThrowIfNull(viewFactory);
        _nodes = nodes;
        _modelCount = modelCount;
        _viewFactory = viewFactory;
    }

    public int Count => _nodes.Count;

    public TView this[TKey key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            return ResolveByKeyValue(NormalizeKey(key));
        }
    }

    public TView AtIndex(int index)
    {
        ValidateIndex(index);
        return _viewFactory(_nodes[index]);
    }

    public TView ByPathKey(string discriminator)
    {
        ArgumentNullException.ThrowIfNull(discriminator);
        if (!ParallelGeneratedKeyText.TryUnescapeXPathLikeDiscriminator(discriminator, out var keyText))
        {
            keyText = discriminator;
        }

        return ResolveByKeyText(keyText, "generated dictionary");
    }

    public ParallelGeneratedModelList<TElement, TView> SelectModel(int modelIndex)
    {
        ValidateModelIndex(modelIndex);
        return new ParallelGeneratedModelList<TElement, TView>(_nodes, _viewFactory, modelIndex);
    }

    public IEnumerator<TView> GetEnumerator()
    {
        for (var index = 0; index < _nodes.Count; index++)
        {
            yield return _viewFactory(_nodes[index]);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private TView ResolveByKeyText(string keyText, string containerName)
    {
        if (GetKeyIndexCache().TryGetValue(keyText, out var index))
        {
            return _viewFactory(_nodes[index]);
        }

        throw new CompareExecutionException(
            CompareIssueCode.KeyNotFound,
            $"key '{keyText}' was not found in {containerName}.");
    }

    private TView ResolveByKeyValue(object key)
    {
        if (GetKeyValueIndexCache().TryGetValue(key, out var index))
        {
            return _viewFactory(_nodes[index]);
        }

        throw new CompareExecutionException(
            CompareIssueCode.KeyNotFound,
            $"key '{key}' was not found in generated dictionary.");
    }

    private void ValidateIndex(int index)
    {
        if (index >= 0 && index < _nodes.Count)
        {
            return;
        }

        throw new CompareExecutionException(
            CompareIssueCode.ModelIndexOutOfRange,
            $"dictionary index '{index}' is out of range for count '{_nodes.Count}'.");
    }

    private void ValidateModelIndex(int modelIndex)
    {
        if (modelIndex >= 0 && modelIndex < _modelCount)
        {
            return;
        }

        throw new CompareExecutionException(
            CompareIssueCode.ModelIndexOutOfRange,
            $"model index '{modelIndex}' is out of range for count '{_modelCount}'.");
    }

    private Dictionary<string, int> GetKeyIndexCache()
    {
        if (_keyIndexCache is not null)
        {
            return _keyIndexCache;
        }

        _keyIndexCache = ParallelGeneratedKeyText.CreateKeyIndexCache(_nodes);
        return _keyIndexCache;
    }

    private Dictionary<object, int> GetKeyValueIndexCache()
    {
        if (_keyValueIndexCache is not null)
        {
            return _keyValueIndexCache;
        }

        var comparer = _nodes.FirstOrDefault(static node => node.KeyComparer is not null)?.KeyComparer
            ?? EqualityComparer<object>.Default;
        var keyValueIndexCache = new Dictionary<object, int>(comparer);
        for (var index = 0; index < _nodes.Count; index++)
        {
            var keyValue = _nodes[index].KeyValue;
            if (keyValue is not null)
            {
                keyValueIndexCache.TryAdd(keyValue, index);
            }
        }

        _keyValueIndexCache = keyValueIndexCache;
        return _keyValueIndexCache;
    }

    private static object NormalizeKey(object key) =>
        key is DateTime dateTime ? dateTime.ToUniversalTime() : key;
}

public sealed class ParallelGeneratedList<TElement, TView> : IReadOnlyList<TView>
{
    private readonly IReadOnlyList<ParallelNode<TElement>> _nodes;
    private readonly int _modelCount;
    private readonly Func<ParallelNode<TElement>, TView> _viewFactory;
    private Dictionary<string, int>? _keyIndexCache;

    public ParallelGeneratedList(IReadOnlyList<ParallelNode<TElement>> nodes, Func<ParallelNode<TElement>, TView> viewFactory)
        : this(nodes, nodes.Count > 0 ? nodes[0].Count : 0, viewFactory)
    {
    }

    public ParallelGeneratedList(
        IReadOnlyList<ParallelNode<TElement>> nodes,
        int modelCount,
        Func<ParallelNode<TElement>, TView> viewFactory)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentOutOfRangeException.ThrowIfNegative(modelCount);
        ArgumentNullException.ThrowIfNull(viewFactory);
        _nodes = nodes;
        _modelCount = modelCount;
        _viewFactory = viewFactory;
    }

    public int Count => _nodes.Count;

    public TView this[int index]
    {
        get
        {
            ValidateIndex(index);
            return _viewFactory(_nodes[index]);
        }
    }

    public TView this[string keyText]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(keyText);

            var keyIndexCache = GetKeyIndexCache();
            if (ParallelGeneratedKeyText.TryUnescapeXPathLikeDiscriminator(keyText, out var unescapedKeyText)
                && keyIndexCache.TryGetValue(unescapedKeyText, out var index))
            {
                return _viewFactory(_nodes[index]);
            }

            if (keyIndexCache.TryGetValue(keyText, out index))
            {
                return _viewFactory(_nodes[index]);
            }

            throw new CompareExecutionException(
                CompareIssueCode.KeyNotFound,
                $"key '{keyText}' was not found in generated list.");
        }
    }

    public ParallelGeneratedModelList<TElement, TView> SelectModel(int modelIndex)
    {
        ValidateModelIndex(modelIndex);
        return new ParallelGeneratedModelList<TElement, TView>(_nodes, _viewFactory, modelIndex);
    }

    private void ValidateIndex(int index)
    {
        if (index >= 0 && index < _nodes.Count)
        {
            return;
        }

        throw new CompareExecutionException(
            CompareIssueCode.ModelIndexOutOfRange,
            $"list index '{index}' is out of range for count '{_nodes.Count}'.");
    }

    private void ValidateModelIndex(int modelIndex)
    {
        if (modelIndex >= 0 && modelIndex < _modelCount)
        {
            return;
        }

        throw new CompareExecutionException(
            CompareIssueCode.ModelIndexOutOfRange,
            $"model index '{modelIndex}' is out of range for count '{_modelCount}'.");
    }

    private Dictionary<string, int> GetKeyIndexCache()
    {
        if (_keyIndexCache is not null)
        {
            return _keyIndexCache;
        }

        _keyIndexCache = ParallelGeneratedKeyText.CreateKeyIndexCache(_nodes);
        return _keyIndexCache;
    }

    public TView AtIndex(int index) => this[index];

    public TView ByPathKey(string discriminator) => this[discriminator];

    public IEnumerator<TView> GetEnumerator()
    {
        for (var index = 0; index < _nodes.Count; index++)
        {
            yield return _viewFactory(_nodes[index]);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class ParallelGeneratedKeyText
{
    public static Dictionary<string, int> CreateKeyIndexCache<TElement>(IReadOnlyList<ParallelNode<TElement>> nodes)
    {
        var keyIndexCache = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < nodes.Count; index++)
        {
            var keyText = nodes[index].KeyText;
            if (keyText is not null)
            {
                keyIndexCache.TryAdd(keyText, index);
            }
        }

        return keyIndexCache;
    }

    public static bool TryUnescapeXPathLikeDiscriminator(string keyText, out string unescapedKeyText)
    {
        var builder = new System.Text.StringBuilder(keyText.Length);
        var changed = false;
        for (var index = 0; index < keyText.Length; index++)
        {
            var current = keyText[index];
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index + 1 >= keyText.Length)
            {
                unescapedKeyText = string.Empty;
                return false;
            }

            var escaped = keyText[++index];
            if (escaped is not (']' or '\\' or '#'))
            {
                unescapedKeyText = string.Empty;
                return false;
            }

            builder.Append(escaped);
            changed = true;
        }

        unescapedKeyText = changed ? builder.ToString() : keyText;
        return changed;
    }
}

public sealed class ParallelGeneratedModelList<TElement, TView> : IReadOnlyList<TView>
{
    private readonly IReadOnlyList<ParallelNode<TElement>> _nodes;
    private readonly Func<ParallelNode<TElement>, TView> _viewFactory;
    private readonly int[] _selectedNodeIndexes;

    internal ParallelGeneratedModelList(
        IReadOnlyList<ParallelNode<TElement>> nodes,
        Func<ParallelNode<TElement>, TView> viewFactory,
        int modelIndex)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(viewFactory);
        _nodes = nodes;
        _viewFactory = viewFactory;

        var selectedNodeIndexes = new List<int>(_nodes.Count);
        for (var index = 0; index < _nodes.Count; index++)
        {
            if (_nodes[index].GetPresenceState(modelIndex) != NodePresenceState.Missing)
            {
                selectedNodeIndexes.Add(index);
            }
        }

        _selectedNodeIndexes = [.. selectedNodeIndexes];
    }

    public int Count => _selectedNodeIndexes.Length;

    public TView this[int index]
    {
        get
        {
            ValidateIndex(index);
            return _viewFactory(_nodes[_selectedNodeIndexes[index]]);
        }
    }

    public IEnumerator<TView> GetEnumerator()
    {
        for (var index = 0; index < _selectedNodeIndexes.Length; index++)
        {
            yield return _viewFactory(_nodes[_selectedNodeIndexes[index]]);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void ValidateIndex(int index)
    {
        if (index >= 0 && index < _selectedNodeIndexes.Length)
        {
            return;
        }

        throw new CompareExecutionException(
            CompareIssueCode.ModelIndexOutOfRange,
            $"list index '{index}' is out of range for count '{_selectedNodeIndexes.Length}'.");
    }
}

public sealed class ParallelGeneratedValue<TModel, TValue>
{
    private readonly ParallelNode<TModel> _node;
    private readonly Func<TModel, TValue> _getter;

    public ParallelGeneratedValue(ParallelNode<TModel> node, Func<TModel, TValue> getter)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(getter);
        _node = node;
        _getter = getter;
    }

    public TValue this[int modelIndex] => ResolveValue(modelIndex, out _);

    public ValueState GetState(int modelIndex)
    {
        var selectedValue = ResolveValue(modelIndex, out var selectedPresence);
        return GetState(
            modelIndex,
            selectedValue,
            selectedPresence,
            index =>
            {
                var value = ResolveValue(index, out var presence);
                return new ResolvedGeneratedValue(value, presence);
            });
    }

    private ValueState GetState(
        int modelIndex,
        TValue selectedValue,
        NodePresenceState selectedPresence,
        Func<int, ResolvedGeneratedValue> getResolvedValue)
    {
        if (selectedPresence == NodePresenceState.Missing)
        {
            return ValueState.Missing;
        }

        if (_node.Count <= 1)
        {
            return ValueState.Missing;
        }

        var matched = true;
        for (var index = 0; index < _node.Count; index++)
        {
            if (index == modelIndex)
            {
                continue;
            }

            var other = getResolvedValue(index);
            if (other.Presence == NodePresenceState.Missing)
            {
                matched = false;
                break;
            }

            if (other.Presence != selectedPresence)
            {
                matched = false;
                break;
            }

            if (selectedPresence == NodePresenceState.PresentValue
                && !EqualityComparer<TValue>.Default.Equals(selectedValue, other.Value))
            {
                matched = false;
                break;
            }
        }

        return ValueStateExtensions.ToComparisonState(hasComparisonTarget: true, matched);
    }

    public ParallelGeneratedValue<TModel, TNext> Select<TNext>(Func<TValue, TNext> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return new ParallelGeneratedValue<TModel, TNext>(
            _node,
            model =>
            {
                var value = _getter(model);
                if (value is null)
                {
                    return default!;
                }

                return selector(value);
            });
    }

    public override string ToString()
    {
        var resolvedValues = new ResolvedGeneratedValue[_node.Count];
        for (var modelIndex = 0; modelIndex < resolvedValues.Length; modelIndex++)
        {
            var value = ResolveValue(modelIndex, out var presence);
            resolvedValues[modelIndex] = new ResolvedGeneratedValue(value, presence);
        }

        return ParallelDisplayFormatter.FormatSlots(
            _node.Count,
            modelIndex =>
            {
                var selected = resolvedValues[modelIndex];
                return new ParallelDisplaySlot(
                    selected.Value,
                    GetState(modelIndex, selected.Value, selected.Presence, index => resolvedValues[index]));
            });
    }

    private readonly struct ResolvedGeneratedValue
    {
        public ResolvedGeneratedValue(TValue value, NodePresenceState presence)
        {
            Value = value;
            Presence = presence;
        }

        public TValue Value { get; }

        public NodePresenceState Presence { get; }
    }

    private TValue ResolveValue(int modelIndex, out NodePresenceState state)
    {
        state = _node.GetPresenceState(modelIndex);
        if (state == NodePresenceState.Missing)
        {
            return default!;
        }

        var model = _node[modelIndex];
        if (model is null)
        {
            state = NodePresenceState.PresentNull;
            return default!;
        }

        var value = _getter(model);
        if (value is null)
        {
            state = NodePresenceState.PresentNull;
            return default!;
        }

        state = NodePresenceState.PresentValue;
        return value;
    }
}

public static class ParallelGeneratedRuntime
{
    public static ParallelNode<T> RequireNode<T>(Parallel<T> node, string apiName)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is ParallelNode<T> parallelNode)
        {
            return parallelNode;
        }

        throw new ArgumentException(
            $"{apiName} can be used only with compare result nodes.",
            nameof(node));
    }

    public static ParallelNode<TMember> RequireMemberNode<TParent, TMember>(
        ParallelNode<TParent> node,
        string memberName,
        string apiName)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(memberName);
        ArgumentException.ThrowIfNullOrEmpty(apiName);

        var internalNode = (IParallelNodeInternal)node;
        if (!internalNode.TryGetMemberNode(memberName, out var rawMemberNode))
        {
            throw new ArgumentException(
                $"{apiName} cannot find generated member node '{memberName}'.",
                nameof(memberName));
        }

        if (rawMemberNode is ParallelNode<TMember> memberNode)
        {
            return memberNode;
        }

        throw new ArgumentException(
            $"{apiName} can be used only with generated member node '{memberName}'.",
            nameof(memberName));
    }
}
