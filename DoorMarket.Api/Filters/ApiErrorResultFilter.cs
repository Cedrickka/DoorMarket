using DoorMarket.Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DoorMarket.Api.Filters;

public sealed class ApiErrorResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var normalized = NormalizeResult(context.Result, context.HttpContext);
        if (normalized is not null)
        {
            context.Result = normalized;
        }

        await next();
    }

    private static IActionResult? NormalizeResult(IActionResult result, HttpContext context)
    {
        switch (result)
        {
            case ObjectResult objectResult:
            {
                var statusCode = ResolveObjectResultStatus(objectResult, context);
                if (statusCode < 400)
                {
                    return null;
                }

                var payload = ApiErrorFactory.FromStatusCode(statusCode, context, objectResult.Value);
                return new ObjectResult(payload)
                {
                    StatusCode = statusCode
                };
            }
            case JsonResult jsonResult:
            {
                var statusCode = jsonResult.StatusCode ?? context.Response.StatusCode;
                if (statusCode < 400)
                {
                    return null;
                }

                var payload = ApiErrorFactory.FromStatusCode(statusCode, context, jsonResult.Value);
                return new ObjectResult(payload)
                {
                    StatusCode = statusCode
                };
            }
            case StatusCodeResult statusCodeResult when statusCodeResult.StatusCode >= 400:
            {
                var payload = ApiErrorFactory.FromStatusCode(statusCodeResult.StatusCode, context);
                return new ObjectResult(payload)
                {
                    StatusCode = statusCodeResult.StatusCode
                };
            }
            default:
                return null;
        }
    }

    private static int ResolveObjectResultStatus(ObjectResult objectResult, HttpContext context)
    {
        if (objectResult.StatusCode.HasValue)
        {
            return objectResult.StatusCode.Value;
        }

        if (objectResult.Value is ProblemDetails problemDetails && problemDetails.Status.HasValue)
        {
            return problemDetails.Status.Value;
        }

        return context.Response.StatusCode;
    }
}
