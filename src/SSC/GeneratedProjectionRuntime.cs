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

public sealed class ParallelGeneratedList<TElement, TView> : IReadOnlyList<TView>
{
    private readonly IReadOnlyList<ParallelNode<TElement>> _nodes;
    private readonly Func<ParallelNode<TElement>, TView> _viewFactory;

    public ParallelGeneratedList(IReadOnlyList<ParallelNode<TElement>> nodes, Func<ParallelNode<TElement>, TView> viewFactory)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(viewFactory);
        _nodes = nodes;
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

    public IEnumerator<TView> GetEnumerator()
    {
        for (var index = 0; index < _nodes.Count; index++)
        {
            yield return _viewFactory(_nodes[index]);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
        _ = ResolveValue(modelIndex, out var state);
        return state;
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

    private TValue ResolveValue(int modelIndex, out ValueState state)
    {
        state = _node.GetState(modelIndex);
        if (state == ValueState.Missing)
        {
            return default!;
        }

        var model = _node[modelIndex];
        if (model is null)
        {
            state = ValueState.PresentNull;
            return default!;
        }

        var value = _getter(model);
        if (value is null)
        {
            state = ValueState.PresentNull;
            return default!;
        }

        state = ValueState.PresentValue;
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
}
