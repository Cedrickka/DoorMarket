namespace DoorMarket.Api.Models;

public sealed class ApiErrorResponse
{
    public required string code { get; init; }
    public required string message { get; init; }
    public required int status { get; init; }
    public required string correlationId { get; init; }
    public string? path { get; init; }
    public object? details { get; init; }

    // Backward-compat for existing web/mobile clients reading `error`.
    public string error => message;
}

