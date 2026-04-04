using System.Collections.Concurrent;
using System.Reflection;

namespace SSC.Internal;

internal static class TypeMetadataResolver
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> CompareKeyCache = new();

    public static PropertyInfo[] GetComparableProperties(Type type)
    {
        return PropertyCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetMethod is not null)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => property.GetCustomAttribute<CompareIgnoreAttribute>() is null)
                .ToArray());
    }

    public static PropertyInfo? GetCompareKeyProperty(Type type)
    {
        return CompareKeyCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(property => property.GetCustomAttribute<CompareKeyAttribute>() is not null));
    }
}
