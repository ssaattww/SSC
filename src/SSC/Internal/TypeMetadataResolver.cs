using System.Collections.Concurrent;
using System.Reflection;

namespace SSC.Internal;

internal static class TypeMetadataResolver
{
    private static readonly ConcurrentDictionary<Type, ComparableMember[]> MemberCache = new();
    private static readonly ConcurrentDictionary<Type, ComparableMember?> CompareKeyCache = new();

    public static ComparableMember[] GetComparableMembers(Type type)
    {
        return MemberCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetMethod is not null)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => property.GetCustomAttribute<CompareIgnoreAttribute>() is null)
                .Select(property => new ComparableMember(property))
                .Concat(t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => field.GetCustomAttribute<CompareIgnoreAttribute>() is null)
                    .Select(field => new ComparableMember(field)))
                .ToArray());
    }

    public static ComparableMember? GetCompareKeyMember(Type type)
    {
        return CompareKeyCache.GetOrAdd(type, static t =>
        {
            var property = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(property => property.GetCustomAttribute<CompareKeyAttribute>() is not null);
            if (property is not null)
            {
                return new ComparableMember(property);
            }

            var field = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(field => field.GetCustomAttribute<CompareKeyAttribute>() is not null);
            return field is null ? null : new ComparableMember(field);
        });
    }
}

internal sealed class ComparableMember
{
    private readonly PropertyInfo? _property;
    private readonly FieldInfo? _field;

    public ComparableMember(PropertyInfo property)
    {
        _property = property;
        Name = property.Name;
        MemberType = property.PropertyType;
    }

    public ComparableMember(FieldInfo field)
    {
        _field = field;
        Name = field.Name;
        MemberType = field.FieldType;
    }

    public string Name { get; }

    public Type MemberType { get; }

    public object? GetValue(object instance) =>
        _property is not null ? _property.GetValue(instance) : _field!.GetValue(instance);
}
