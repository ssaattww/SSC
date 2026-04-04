namespace SSC;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CompareIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CompareKeyAttribute : Attribute
{
}
