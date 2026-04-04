namespace SSC;

public sealed class CompareInputException : Exception
{
    public CompareInputException(CompareIssueCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public CompareIssueCode Code { get; }
}

public sealed class CompareExecutionException : Exception
{
    public CompareExecutionException(CompareIssueCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public CompareIssueCode Code { get; }
}
