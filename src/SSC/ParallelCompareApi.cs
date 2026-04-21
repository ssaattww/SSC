using System.Collections;
using System.Reflection;
using SSC.Internal;

namespace SSC;

public static class ParallelCompareApi
{
    private const string NullKeyText = "<null>";

    private static readonly MethodInfo BuildNodeMethod = typeof(ParallelCompareApi)
        .GetMethod(nameof(BuildNodeGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static CompareResult<T> Compare<T>(IReadOnlyList<T> models, CompareConfiguration? configuration = null)
    {
        var config = configuration ?? new CompareConfiguration();
        var context = new CompareContext(config);
        if (context.IsTraceEnabled)
        {
            context.Trace("start", typeof(T).Name, ("rootType", typeof(T)), ("modelCount", models?.Count), ("strictMode", config.StrictMode));
        }

        if (models is null || models.Count == 0)
        {
            context.RecordInputError(
                CompareIssueCode.InputModelListEmpty,
                typeof(T).Name,
                null,
                null,
                "models must contain at least one element.");

            return BuildResult<T>(root: null, context.Issues);
        }

        for (var modelIndex = 0; modelIndex < models.Count; modelIndex++)
        {
            if (models[modelIndex] is not null)
            {
                continue;
            }

            context.RecordInputError(
                CompareIssueCode.InputModelNullElement,
                typeof(T).Name,
                modelIndex,
                null,
                "models contains null element.");
        }

        if (context.HasErrors)
        {
            return BuildResult<T>(root: null, context.Issues);
        }

        var slots = new NodeSlot[models.Count];
        for (var index = 0; index < models.Count; index++)
        {
            slots[index] = new NodeSlot(models[index], NodePresenceState.PresentValue);
        }

        var root = (ParallelNode<T>)BuildNode(typeof(T), slots, typeof(T).Name, context, keyText: null);
        return BuildResult(root, context.Issues);
    }

    private static CompareResult<T> BuildResult<T>(Parallel<T>? root, IReadOnlyList<CompareIssue> issues)
    {
        return new CompareResult<T>
        {
            Root = root,
            Issues = issues,
            HasError = issues.Any(issue => issue.Level == CompareIssueLevel.Error),
        };
    }

    private static IParallelNode BuildNode(Type nodeType, NodeSlot[] slots, string path, CompareContext context, string? keyText)
    {
        var generic = BuildNodeMethod.MakeGenericMethod(nodeType);
        try
        {
            return (IParallelNode)generic.Invoke(null, [slots, path, context, keyText])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is CompareInputException)
        {
            throw ex.InnerException;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is CompareExecutionException)
        {
            throw ex.InnerException;
        }
    }

    private static IParallelNode BuildNodeGeneric<TNode>(NodeSlot[] slots, string path, CompareContext context, string? keyText)
    {
        var typedValues = new TNode?[slots.Length];
        var states = new NodePresenceState[slots.Length];

        for (var index = 0; index < slots.Length; index++)
        {
            states[index] = slots[index].State;
            typedValues[index] = slots[index].State == NodePresenceState.PresentValue
                ? (TNode?)slots[index].Value
                : default;
        }

        var isScalarNode = IsScalarType(typeof(TNode));
        var node = new ParallelNode<TNode>(typedValues, states, keyText, isScalarNode);
        if (isScalarNode)
        {
            if (context.IsTraceEnabled)
            {
                context.Trace("node", path, ("nodeType", typeof(TNode)), ("nodeKind", "Scalar"), ("keyText", keyText));
            }
            return node;
        }

        if (context.IsTraceEnabled)
        {
            context.Trace("node", path, ("nodeType", typeof(TNode)), ("nodeKind", "Object"), ("keyText", keyText));
        }

        foreach (var property in TypeMetadataResolver.GetComparableProperties(typeof(TNode)))
        {
            var propertyPath = $"{path}.{property.Name}";
            if (context.IsTraceEnabled)
            {
                context.Trace(
                    "metadata",
                    propertyPath,
                    ("declaredType", property.PropertyType),
                    ("container", GetContainerCategory(property.PropertyType)));
            }

            var memberSlots = BuildMemberSlots(slots, property, propertyPath, context);

            if (TryGetDictionaryTypes(property.PropertyType, out var keyType, out var elementType))
            {
                if (context.IsTraceEnabled)
                {
                    context.Trace("metadata", propertyPath, ("keyType", keyType), ("valueType", elementType));
                }
                var children = BuildDictionaryChildren(memberSlots, keyType, elementType, propertyPath, context);
                node.SetChildren(property.Name, children, memberSlots.Select(slot => slot.State).ToArray());
                continue;
            }

            if (TryGetSequenceElementType(property.PropertyType, out elementType))
            {
                if (context.IsTraceEnabled)
                {
                    context.Trace("metadata", propertyPath, ("elementType", elementType));
                }
                var children = BuildSequenceChildren(memberSlots, property.PropertyType, elementType, propertyPath, context);
                node.SetChildren(property.Name, children, memberSlots.Select(slot => slot.State).ToArray());
                continue;
            }

            var memberNode = BuildNode(property.PropertyType, memberSlots, propertyPath, context, keyText: null);
            node.SetMemberNode(property.Name, memberNode);
        }

        return node;
    }

    private static NodeSlot[] BuildMemberSlots(
        NodeSlot[] parentSlots,
        PropertyInfo property,
        string path,
        CompareContext context)
    {
        var slots = new NodeSlot[parentSlots.Length];
        for (var modelIndex = 0; modelIndex < parentSlots.Length; modelIndex++)
        {
            if (parentSlots[modelIndex].State == NodePresenceState.Missing)
            {
                slots[modelIndex] = NodeSlot.Missing;
                continue;
            }

            if (parentSlots[modelIndex].State == NodePresenceState.PresentNull || parentSlots[modelIndex].Value is null)
            {
                slots[modelIndex] = NodeSlot.PresentNull;
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(parentSlots[modelIndex].Value);
            }
            catch (Exception ex)
            {
                context.RecordExecutionError(
                    CompareIssueCode.ReflectionMetadataBuildFailed,
                    path,
                    modelIndex,
                    null,
                    $"failed to get property '{property.Name}': {ex.Message}");
                slots[modelIndex] = NodeSlot.Missing;
                continue;
            }

            slots[modelIndex] = value is null ? NodeSlot.PresentNull : NodeSlot.PresentValue(value);
        }

        return slots;
    }

    private static IReadOnlyList<IParallelNode> BuildDictionaryChildren(
        NodeSlot[] containerSlots,
        Type keyType,
        Type elementType,
        string path,
        CompareContext context)
    {
        var comparer = new KeyComparer(keyType, context.Configuration.StringKeyComparison);
        var maps = new Dictionary<object, object?>[containerSlots.Length];
        var union = new HashSet<object>(comparer);
        var keyTexts = new Dictionary<object, string>(comparer);

        for (var modelIndex = 0; modelIndex < containerSlots.Length; modelIndex++)
        {
            maps[modelIndex] = new Dictionary<object, object?>(comparer);
            if (containerSlots[modelIndex].State != NodePresenceState.PresentValue || containerSlots[modelIndex].Value is null)
            {
                continue;
            }

            var rawContainer = containerSlots[modelIndex].Value;

            if (rawContainer is null)
            {
                if (context.IsTraceEnabled)
                {
                    context.Trace("container", path, ("modelIndex", modelIndex), ("container", "Dictionary"), ("runtimeType", "<null>"), ("materializedCount", 0));
                }
                continue;
            }

            if (context.IsTraceEnabled)
            {
                context.Trace(
                    "container",
                    path,
                    ("modelIndex", modelIndex),
                    ("container", "Dictionary"),
                    ("runtimeType", rawContainer.GetType()),
                    ("keyType", keyType),
                    ("valueType", elementType));
            }

            foreach (var (entryKey, entryValue) in EnumerateDictionary(rawContainer))
            {
                if (entryKey is null)
                {
                    context.RecordExecutionError(
                        CompareIssueCode.CompareKeyValueIsNull,
                        path,
                        modelIndex,
                        NullKeyText,
                        "dictionary key is null.");
                    continue;
                }

                var normalizedKey = NormalizeKey(entryKey);
                UpdateCanonicalKeyText(
                    keyTexts,
                    normalizedKey,
                    KeyToText(entryKey),
                    keyType,
                    context.Configuration.StringKeyComparison);
                if (maps[modelIndex].ContainsKey(normalizedKey))
                {
                    context.RecordExecutionError(
                        CompareIssueCode.DuplicateCompareKeyDetected,
                        path,
                        modelIndex,
                        keyTexts[normalizedKey],
                        $"duplicate compare key '{normalizedKey}'.");
                    continue;
                }

                maps[modelIndex][normalizedKey] = entryValue;
                union.Add(normalizedKey);
            }
        }

        var orderedKeys = union.OrderBy(key => key, comparer).ToArray();
        var children = new List<IParallelNode>(orderedKeys.Length);
        foreach (var key in orderedKeys)
        {
            var slots = new NodeSlot[containerSlots.Length];
            for (var modelIndex = 0; modelIndex < containerSlots.Length; modelIndex++)
            {
                if (!maps[modelIndex].TryGetValue(key, out var value))
                {
                    slots[modelIndex] = NodeSlot.Missing;
                    continue;
                }

                slots[modelIndex] = value is null ? NodeSlot.PresentNull : NodeSlot.PresentValue(value);
            }

            var childNode = BuildNode(
                elementType,
                slots,
                path,
                context,
                keyText: keyTexts.TryGetValue(key, out var keyText) ? keyText : KeyToText(key));
            children.Add(childNode);
        }

        return children;
    }

    private static IReadOnlyList<IParallelNode> BuildSequenceChildren(
        NodeSlot[] containerSlots,
        Type containerType,
        Type elementType,
        string path,
        CompareContext context)
    {
        var compareKeyProperty = TypeMetadataResolver.GetCompareKeyProperty(elementType);
        if (compareKeyProperty is null)
        {
            context.RecordExecutionError(
                CompareIssueCode.CompareKeyNotFoundOnSequenceElement,
                path,
                null,
                null,
                $"sequence element type '{elementType.Name}' requires [CompareKey].");
            return Array.Empty<IParallelNode>();
        }

        var containerCategory = GetContainerCategory(containerType);
        if (context.IsTraceEnabled)
        {
            context.Trace(
                "container",
                path,
                ("container", containerCategory),
                ("elementType", elementType),
                ("compareKey", compareKeyProperty.Name));
        }

        var keyType = compareKeyProperty.PropertyType;
        var comparer = new KeyComparer(keyType, context.Configuration.StringKeyComparison);
        var maps = new Dictionary<object, object?>[containerSlots.Length];
        var union = new HashSet<object>(comparer);
        var keyTexts = new Dictionary<object, string>(comparer);

        for (var modelIndex = 0; modelIndex < containerSlots.Length; modelIndex++)
        {
            maps[modelIndex] = new Dictionary<object, object?>(comparer);
            if (containerSlots[modelIndex].State != NodePresenceState.PresentValue || containerSlots[modelIndex].Value is null)
            {
                continue;
            }

            var rawContainer = containerSlots[modelIndex].Value;

            if (rawContainer is null)
            {
                if (context.IsTraceEnabled)
                {
                    context.Trace(
                        "container",
                        path,
                        ("modelIndex", modelIndex),
                        ("container", containerCategory),
                        ("runtimeType", "<null>"),
                        ("materializedCount", 0));
                }
                continue;
            }

            if (rawContainer is not IEnumerable enumerable)
            {
                context.RecordExecutionError(
                    CompareIssueCode.UnsupportedContainerType,
                    path,
                    modelIndex,
                    null,
                    $"container type '{rawContainer.GetType().Name}' is not supported.");
                continue;
            }

            var materialized = enumerable.Cast<object?>().ToList();
            if (context.IsTraceEnabled)
            {
                context.Trace(
                    "container",
                    path,
                    ("modelIndex", modelIndex),
                    ("container", containerCategory),
                    ("runtimeType", rawContainer.GetType()),
                    ("materializedCount", materialized.Count));
            }
            foreach (var element in materialized)
            {
                if (element is null)
                {
                    context.RecordExecutionError(
                        CompareIssueCode.CompareKeyValueIsNull,
                        path,
                        modelIndex,
                        NullKeyText,
                        "sequence element is null.");
                    continue;
                }

                object? key;
                try
                {
                    key = compareKeyProperty.GetValue(element);
                }
                catch (Exception ex)
                {
                    context.RecordExecutionError(
                        CompareIssueCode.ReflectionMetadataBuildFailed,
                        path,
                        modelIndex,
                        null,
                        $"failed to read compare key '{compareKeyProperty.Name}': {ex.Message}");
                    continue;
                }

                if (key is null)
                {
                    context.RecordExecutionError(
                        CompareIssueCode.CompareKeyValueIsNull,
                        path,
                        modelIndex,
                        NullKeyText,
                        $"compare key '{compareKeyProperty.Name}' is null.");
                    continue;
                }

                var normalizedKey = NormalizeKey(key);
                UpdateCanonicalKeyText(
                    keyTexts,
                    normalizedKey,
                    KeyToText(key),
                    keyType,
                    context.Configuration.StringKeyComparison);
                if (maps[modelIndex].ContainsKey(normalizedKey))
                {
                    context.RecordExecutionError(
                        CompareIssueCode.DuplicateCompareKeyDetected,
                        path,
                        modelIndex,
                        keyTexts[normalizedKey],
                        $"duplicate compare key '{normalizedKey}'.");
                    continue;
                }

                maps[modelIndex][normalizedKey] = element;
                union.Add(normalizedKey);
            }
        }

        var orderedKeys = union.OrderBy(key => key, comparer).ToArray();
        var children = new List<IParallelNode>(orderedKeys.Length);
        foreach (var key in orderedKeys)
        {
            var slots = new NodeSlot[containerSlots.Length];
            for (var modelIndex = 0; modelIndex < containerSlots.Length; modelIndex++)
            {
                if (!maps[modelIndex].TryGetValue(key, out var value))
                {
                    slots[modelIndex] = NodeSlot.Missing;
                    continue;
                }

                slots[modelIndex] = value is null ? NodeSlot.PresentNull : NodeSlot.PresentValue(value);
            }

            var childNode = BuildNode(
                elementType,
                slots,
                path,
                context,
                keyText: keyTexts.TryGetValue(key, out var keyText) ? keyText : KeyToText(key));
            children.Add(childNode);
        }

        return children;
    }

    private static IEnumerable<(object? Key, object? Value)> EnumerateDictionary(object rawContainer)
    {
        if (rawContainer is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return (entry.Key, entry.Value);
            }

            yield break;
        }

        if (rawContainer is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var entry in enumerable)
        {
            if (entry is null)
            {
                continue;
            }

            var type = entry.GetType();
            var key = type.GetProperty("Key")?.GetValue(entry);
            var value = type.GetProperty("Value")?.GetValue(entry);
            yield return (key, value);
        }
    }

    private static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        if (TryGetGenericInterface(type, typeof(IDictionary<,>), out var dictionaryType)
            || TryGetGenericInterface(type, typeof(IReadOnlyDictionary<,>), out dictionaryType))
        {
            var arguments = dictionaryType.GetGenericArguments();
            keyType = arguments[0];
            valueType = arguments[1];
            return true;
        }

        keyType = null!;
        valueType = null!;
        return false;
    }

    private static bool TryGetSequenceElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = null!;
            return false;
        }

        if (TryGetDictionaryTypes(type, out _, out _))
        {
            elementType = null!;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (TryGetGenericInterface(type, typeof(IEnumerable<>), out var enumerableType))
        {
            elementType = enumerableType.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private static bool TryGetGenericInterface(Type concreteType, Type openGeneric, out Type match)
    {
        if (concreteType.IsGenericType && concreteType.GetGenericTypeDefinition() == openGeneric)
        {
            match = concreteType;
            return true;
        }

        match = concreteType.GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == openGeneric)!;

        return match is not null;
    }

    private static string GetContainerCategory(Type type)
    {
        if (TryGetDictionaryTypes(type, out _, out _))
        {
            return "Dictionary";
        }

        if (type.IsArray)
        {
            return "Array";
        }

        if (type != typeof(string)
            && (TryGetGenericInterface(type, typeof(IList<>), out _)
                || TryGetGenericInterface(type, typeof(IReadOnlyList<>), out _)))
        {
            return "List";
        }

        if (type != typeof(string) && TryGetGenericInterface(type, typeof(IEnumerable<>), out _))
        {
            return "IEnumerable";
        }

        return "ScalarOrObject";
    }

    private static bool IsScalarType(Type type)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(type);
        var actual = nullableUnderlying ?? type;

        return actual.IsPrimitive
            || actual.IsEnum
            || actual == typeof(string)
            || actual == typeof(decimal)
            || actual == typeof(DateTime)
            || actual == typeof(DateTimeOffset)
            || actual == typeof(Guid)
            || actual == typeof(TimeSpan);
    }

    private static object NormalizeKey(object key)
    {
        return key is DateTime dateTime ? dateTime.ToUniversalTime() : key;
    }

    private static string KeyToText(object key)
    {
        return key.ToString() ?? string.Empty;
    }

    private static void UpdateCanonicalKeyText(
        IDictionary<object, string> keyTexts,
        object normalizedKey,
        string candidate,
        Type keyType,
        StringComparison stringKeyComparison)
    {
        if (!keyTexts.TryGetValue(normalizedKey, out var existing))
        {
            keyTexts[normalizedKey] = candidate;
            return;
        }

        keyTexts[normalizedKey] = SelectCanonicalKeyText(existing, candidate, keyType, stringKeyComparison);
    }

    private static string SelectCanonicalKeyText(
        string existing,
        string candidate,
        Type keyType,
        StringComparison stringKeyComparison)
    {
        if (keyType == typeof(string) && stringKeyComparison == StringComparison.OrdinalIgnoreCase)
        {
            return StringComparer.Ordinal.Compare(candidate, existing) < 0 ? candidate : existing;
        }

        return existing;
    }

    private readonly record struct NodeSlot(object? Value, NodePresenceState State)
    {
        public static NodeSlot Missing => new(null, NodePresenceState.Missing);

        public static NodeSlot PresentNull => new(null, NodePresenceState.PresentNull);

        public static NodeSlot PresentValue(object value) => new(value, NodePresenceState.PresentValue);
    }

    private sealed class CompareContext
    {
        private readonly List<CompareIssue> _issues = [];

        public CompareContext(CompareConfiguration configuration)
        {
            Configuration = configuration;
        }

        public CompareConfiguration Configuration { get; }

        public IReadOnlyList<CompareIssue> Issues => _issues;

        public bool HasErrors => _issues.Any(issue => issue.Level == CompareIssueLevel.Error);

        public bool IsTraceEnabled => Configuration.TraceLog is not null;

        public void RecordInputError(CompareIssueCode code, string path, int? modelIndex, string? keyText, string message)
        {
            var issue = CreateIssue(code, path, modelIndex, keyText, message);
            _issues.Add(issue);
            if (IsTraceEnabled)
            {
                Trace("issue", path, ("level", CompareIssueLevel.Error), ("code", code), ("modelIndex", modelIndex), ("keyText", keyText), ("message", message));
            }

            if (Configuration.StrictMode)
            {
                throw new CompareInputException(code, message);
            }
        }

        public void RecordExecutionError(CompareIssueCode code, string path, int? modelIndex, string? keyText, string message)
        {
            var issue = CreateIssue(code, path, modelIndex, keyText, message);
            _issues.Add(issue);
            if (IsTraceEnabled)
            {
                Trace("issue", path, ("level", CompareIssueLevel.Error), ("code", code), ("modelIndex", modelIndex), ("keyText", keyText), ("message", message));
            }

            if (Configuration.StrictMode)
            {
                throw new CompareExecutionException(code, message);
            }
        }

        private static CompareIssue CreateIssue(CompareIssueCode code, string path, int? modelIndex, string? keyText, string message)
        {
            return new CompareIssue
            {
                Level = CompareIssueLevel.Error,
                Code = code,
                Path = path,
                ModelIndex = modelIndex,
                KeyText = keyText,
                Message = message,
            };
        }

        public void Trace(string phase, string path, params (string Key, object? Value)[] fields)
        {
            if (Configuration.TraceLog is null)
            {
                return;
            }

            var parts = new List<string>
            {
                $"phase={FormatTraceValue(phase)}",
                $"path={FormatTraceValue(path)}",
            };

            foreach (var (key, value) in fields)
            {
                parts.Add($"{key}={FormatTraceValue(value)}");
            }

            Configuration.TraceLog(string.Join(" ", parts));
        }

        private static string FormatTraceValue(object? value)
        {
            if (value is null)
            {
                return "<null>";
            }

            var text = value switch
            {
                Type type => type.ToString(),
                bool flag => flag ? "true" : "false",
                Enum enumValue => enumValue.ToString(),
                _ => value.ToString() ?? string.Empty,
            };

            return text.Any(char.IsWhiteSpace) ? $"\"{text}\"" : text;
        }
    }

    private sealed class KeyComparer(Type keyType, StringComparison stringKeyComparison) : IEqualityComparer<object>, IComparer<object>
    {
        private readonly Type _keyType = keyType;
        private readonly StringComparison _stringKeyComparison = stringKeyComparison;

        bool IEqualityComparer<object>.Equals(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (_keyType == typeof(string) && x is string sx && y is string sy)
            {
                return string.Equals(sx, sy, _stringKeyComparison);
            }

            return x.Equals(y);
        }

        public int GetHashCode(object obj)
        {
            if (_keyType == typeof(string) && obj is string text)
            {
                return _stringKeyComparison switch
                {
                    StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase.GetHashCode(text),
                    _ => StringComparer.Ordinal.GetHashCode(text),
                };
            }

            return obj.GetHashCode();
        }

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (_keyType == typeof(string) && x is string sx && y is string sy)
            {
                return _stringKeyComparison switch
                {
                    StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase.Compare(sx, sy),
                    _ => StringComparer.Ordinal.Compare(sx, sy),
                };
            }

            if (x is IComparable comparable && x.GetType() == y.GetType())
            {
                return comparable.CompareTo(y);
            }

            return StringComparer.Ordinal.Compare(x.ToString(), y.ToString());
        }
    }
}
