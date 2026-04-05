namespace SSC;

public static class ValueStateExtensions
{
    public static bool IsMissing(this ValueState state) => state == ValueState.Missing;

    public static bool IsMatched(this ValueState state) => state == ValueState.Matched;

    public static bool IsMismatched(this ValueState state) => state == ValueState.Mismatched;

    internal static ValueState ToComparisonState(bool hasComparisonTarget, bool matched)
    {
        if (!hasComparisonTarget)
        {
            return ValueState.Missing;
        }

        return matched ? ValueState.Matched : ValueState.Mismatched;
    }
}
