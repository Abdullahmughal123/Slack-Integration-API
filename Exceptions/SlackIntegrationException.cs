namespace SlackIntegration.Exceptions;
public class SlackIntegrationException : Exception
{
    public string? ErrorCode { get; }
    public string? ErrorDetails { get; }

    public SlackIntegrationException(string message) : base(message)
    {
    }

    public SlackIntegrationException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public SlackIntegrationException(string message, string errorCode, string errorDetails) : base(message)
    {
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
    }

    public SlackIntegrationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public SlackIntegrationException(string message, string errorCode, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
