using System.Globalization;

namespace SSC;

internal readonly struct ParallelDisplaySlot
{
    public ParallelDisplaySlot(object? value, ValueState state)
    {
        Value = value;
        State = state;
    }

    public object? Value { get; }

    public ValueState State { get; }
}

internal static class ParallelDisplayFormatter
{
    public static string FormatSlots(int count, Func<int, ParallelDisplaySlot> getSlot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(getSlot);

        var slots = new string[count];
        for (var modelIndex = 0; modelIndex < count; modelIndex++)
        {
            var slot = getSlot(modelIndex);
            slots[modelIndex] = FormatSlot(modelIndex, slot.Value, slot.State);
        }

        return string.Join(", ", slots);
    }

    public static string FormatSlot(int modelIndex, object? value, ValueState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(modelIndex);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{modelIndex}]={FormatValue(value, state)}({state})");
    }

    private static string FormatValue(object? value, ValueState state)
    {
        if (state == ValueState.Missing)
        {
            return "<missing>";
        }

        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return string.Create(CultureInfo.InvariantCulture, $"\"{text}\"");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
