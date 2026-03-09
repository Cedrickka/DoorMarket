namespace DoorMarket.Mobile.Http;

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public ApiException(string message, int statusCode, string? errorCode = null) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode ?? string.Empty;
    }
}
