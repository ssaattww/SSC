using System.Dynamic;
using System.Reflection;

namespace SSC;

public static class ParallelDynamicAccessExtensions
{
    public static dynamic AsDynamic<T>(this ParallelNode<T> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return DynamicParallelNodeView.From(node);
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

        if (_internalNode.TryGetChildren(binder.Name, out var nodes))
        {
            result = new DynamicParallelListView(nodes);
            return true;
        }

        var property = _internalNode.ModelType.GetProperty(
            binder.Name,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
        {
            result = null;
            return false;
        }

        result = DynamicParallelValuePathView.FromMember(_node, property.Name);
        return true;
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

internal sealed class DynamicParallelListView : DynamicObject
{
    private readonly IReadOnlyList<IParallelNode> _nodes;

    internal DynamicParallelListView(IReadOnlyList<IParallelNode> nodes)
    {
        _nodes = nodes;
    }

    public int Count => _nodes.Count;

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        if (indexes.Length != 1 || indexes[0] is not int index)
        {
            result = null;
            return false;
        }

        result = DynamicParallelNodeView.From(_nodes[index]);
        return true;
    }
}

internal sealed class DynamicParallelValuePathView : DynamicObject
{
    private readonly IParallelNode _node;
    private readonly string[] _memberPath;

    private DynamicParallelValuePathView(IParallelNode node, string[] memberPath)
    {
        _node = node;
        _memberPath = memberPath;
    }

    public static DynamicParallelValuePathView FromMember(IParallelNode node, string memberName)
    {
        return new DynamicParallelValuePathView(node, [memberName]);
    }

    public ValueState GetState(int modelIndex)
    {
        return _node.GetState(modelIndex);
    }

    public int Count => _node.Count;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = new DynamicParallelValuePathView(_node, [.. _memberPath, binder.Name]);
        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (binder.Name == nameof(GetState) && args is { Length: 1 } && args[0] is int modelIndex)
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

        object? current = _node.GetValue(modelIndex);
        foreach (var memberName in _memberPath)
        {
            if (current is null)
            {
                result = null;
                return true;
            }

            var property = current.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                throw new MissingMemberException(
                    current.GetType().FullName,
                    memberName);
            }

            current = property.GetValue(current);
        }

        result = current;
        return true;
    }
}
