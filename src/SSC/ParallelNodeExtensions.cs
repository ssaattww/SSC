using System.Collections.Generic;
using System.Linq.Expressions;

namespace SSC;

public static class ParallelNodeExtensions
{
    public static IReadOnlyList<ParallelNode<TElement>> Children<TParent, TElement>(
        this ParallelNode<TParent> node,
        Expression<Func<TParent, IEnumerable<TElement>>> memberSelector)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(memberSelector);

        return node.GetChildren<TElement>(GetDirectMemberName(memberSelector));
    }

    public static IReadOnlyList<ParallelNode<TElement>> Children<TParent, TKey, TElement>(
        this ParallelNode<TParent> node,
        Expression<Func<TParent, IReadOnlyDictionary<TKey, TElement>>> memberSelector)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(memberSelector);

        return node.GetChildren<TElement>(GetDirectMemberName(memberSelector));
    }

    private static string GetDirectMemberName<TParent, TMember>(Expression<Func<TParent, TMember>> memberSelector)
    {
        if (memberSelector.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException(
            "memberSelector must be a direct member access expression (e.g. x => x.Items).",
            nameof(memberSelector));
    }
}
