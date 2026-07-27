namespace CodexBar.EngineClient;

public sealed class EngineClientException : Exception
{
    public EngineClientException(string message)
        : base(message)
    {
    }

    public EngineClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
