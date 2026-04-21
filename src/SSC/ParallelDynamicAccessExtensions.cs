using System.Dynamic;
using System.Reflection;

namespace SSC;

public static class ParallelDynamicAccessExtensions
{
    public static dynamic? AsDynamic<T>(this CompareResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Root is null)
        {
            return null;
        }

        if (result.Root is not IParallelNode parallelNode || parallelNode is not IParallelNodeInternal)
        {
            throw new ArgumentException(
                "AsDynamic can be used only with compare result nodes.",
                nameof(result));
        }

        return DynamicParallelNodeView.From(parallelNode);
    }
}

internal sealed class DynamicParallelNodeView : DynamicObject
{
    private readonly IParallelNode _node;
    private readonly IParallelNodeInternal _internalNode;

    private DynamicParallelNodeView(IParallelNode node)
    {
        _node = node;
        _internalNode = (IParallelNodeInternal)node;
    }

    public static object From(IParallelNode node)
    {
        return new DynamicParallelNodeView(node);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (binder.Name == "NodeKeyText")
        {
            result = _node.KeyText;
            return true;
        }

        if (binder.Name == "NodeCount")
        {
            result = _node.Count;
            return true;
        }

        if (_internalNode.TryGetChildren(binder.Name, out var nodes))
        {
            result = new DynamicParallelListView(nodes);
            return true;
        }

        var property = _internalNode.ModelType.GetProperty(
            binder.Name,
            BindingFlags.Instance | BindingFlags.Public);
        if (_internalNode.TryGetMemberNode(binder.Name, out var memberNode) || property is not null)
        {
            result = DynamicParallelValuePathView.FromMember(_node, binder.Name, memberNode);
            return true;
        }

        // Keep legacy names for compatibility, but resolve model members first.
        if (binder.Name == nameof(IParallelNode.KeyText))
        {
            result = _node.KeyText;
            return true;
        }

        if (binder.Name == nameof(IParallelNode.Count))
        {
            result = _node.Count;
            return true;
        }

        result = null;
        return false;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (binder.Name == nameof(IParallelNode.GetState) && args is { Length: 1 } && args[0] is int modelIndex)
        {
            result = _node.GetState(modelIndex);
            return true;
        }

        result = null;
        return false;
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        if (indexes.Length != 1 || indexes[0] is not int modelIndex)
        {
            result = null;
            return false;
        }

        result = _node.GetValue(modelIndex);
        return true;
    }
}

internal sealed class DynamicParallelListView : DynamicObject, IReadOnlyList<object?>
{
    private readonly IReadOnlyList<IParallelNode> _nodes;

    internal DynamicParallelListView(IReadOnlyList<IParallelNode> nodes)
    {
        _nodes = nodes;
    }

    public int Count => _nodes.Count;

    public object? this[int index]
    {
        get
        {
            ValidateIndex(index);
            return DynamicParallelNodeView.From(_nodes[index]);
        }
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        if (indexes.Length != 1 || indexes[0] is not int index)
        {
            result = null;
            return false;
        }

        ValidateIndex(index);
        result = DynamicParallelNodeView.From(_nodes[index]);
        return true;
    }

    public IEnumerator<object?> GetEnumerator()
    {
        for (var index = 0; index < _nodes.Count; index++)
        {
            yield return DynamicParallelNodeView.From(_nodes[index]);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

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
}

internal sealed class DynamicParallelValuePathView : DynamicObject
{
    private readonly IParallelNode _rootNode;
    private readonly IParallelNodeInternal _rootInternalNode;
    private readonly string[] _memberPath;
    private readonly IParallelNode? _materializedNode;
    private readonly IParallelNodeInternal? _materializedInternalNode;

    private DynamicParallelValuePathView(IParallelNode rootNode, string[] memberPath, IParallelNode? materializedNode)
    {
        _rootNode = rootNode;
        _rootInternalNode = (IParallelNodeInternal)rootNode;
        _memberPath = memberPath;
        _materializedNode = materializedNode;
        _materializedInternalNode = materializedNode as IParallelNodeInternal;
    }

    public static DynamicParallelValuePathView FromMember(IParallelNode rootNode, string memberName, IParallelNode? materializedNode = null)
    {
        return new DynamicParallelValuePathView(rootNode, [memberName], materializedNode);
    }

    public ValueState GetState(int modelIndex)
    {
        if (_materializedNode is not null)
        {
            return _materializedNode.GetState(modelIndex);
        }

        var selectedValue = ResolveValue(modelIndex, out var selectedPresence);
        if (selectedPresence == NodePresenceState.Missing)
        {
            return ValueState.Missing;
        }

        if (_rootNode.Count <= 1)
        {
            return ValueState.Missing;
        }

        var matched = true;
        for (var index = 0; index < _rootNode.Count; index++)
        {
            if (index == modelIndex)
            {
                continue;
            }

            var otherValue = ResolveValue(index, out var otherPresence);
            if (otherPresence == NodePresenceState.Missing)
            {
                matched = false;
                break;
            }

            if (otherPresence != selectedPresence)
            {
                matched = false;
                break;
            }

            if (selectedPresence == NodePresenceState.PresentValue
                && !Equals(selectedValue, otherValue))
            {
                matched = false;
                break;
            }
        }

        return ValueStateExtensions.ToComparisonState(hasComparisonTarget: true, matched);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (_materializedNode is not null)
        {
            if (binder.Name == "NodeKeyText")
            {
                result = _materializedNode.KeyText;
                return true;
            }

            if (binder.Name == "NodeCount")
            {
                result = _materializedNode.Count;
                return true;
            }

            if (_materializedInternalNode?.TryGetChildren(binder.Name, out var childNodes) == true)
            {
                result = new DynamicParallelListView(childNodes);
                return true;
            }

            if (_materializedInternalNode?.TryGetMemberNode(binder.Name, out var nextMaterializedNode) == true)
            {
                result = new DynamicParallelValuePathView(_rootNode, [.. _memberPath, binder.Name], nextMaterializedNode);
                return true;
            }

            if (binder.Name == nameof(IParallelNode.KeyText))
            {
                result = _materializedNode.KeyText;
                return true;
            }

            if (binder.Name == nameof(IParallelNode.Count))
            {
                result = _materializedNode.Count;
                return true;
            }
        }

        result = new DynamicParallelValuePathView(_rootNode, [.. _memberPath, binder.Name], materializedNode: null);
        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (binder.Name == nameof(GetState) && args is { Length: 1 } && args[0] is int modelIndex)
        {
            result = GetState(modelIndex);
            return true;
        }

        result = null;
        return false;
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        if (indexes.Length != 1 || indexes[0] is not int modelIndex)
        {
            result = null;
            return false;
        }

        result = ResolveValue(modelIndex, out _);
        return true;
    }

    private object? ResolveValue(int modelIndex, out NodePresenceState state)
    {
        state = _rootInternalNode.GetPresenceState(modelIndex);
        if (state == NodePresenceState.Missing)
        {
            return null;
        }

        object? current = _rootNode.GetValue(modelIndex);
        if (current is null)
        {
            state = NodePresenceState.PresentNull;
            return null;
        }

        foreach (var memberName in _memberPath)
        {
            var property = current.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                throw new MissingMemberException(
                    current.GetType().FullName,
                    memberName);
            }

            current = property.GetValue(current);
            if (current is null)
            {
                state = NodePresenceState.PresentNull;
                return null;
            }
        }

        state = NodePresenceState.PresentValue;
        return current;
    }
}
