using DoorMarket.Api.Errors;

namespace DoorMarket.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unhandled exception after response started. path={Path}", context.Request.Path);
                throw;
            }

            var payload = ApiErrorFactory.FromException(ex, context);
            _logger.LogError(
                ex,
                "Unhandled exception code={Code} correlationId={CorrelationId} path={Path}",
                payload.code,
                payload.correlationId,
                context.Request.Path);

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = payload.status;
            await context.Response.WriteAsJsonAsync(payload);
        }
    }
}
