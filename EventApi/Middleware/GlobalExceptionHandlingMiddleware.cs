using System.ComponentModel.DataAnnotations;
using System.Runtime.ExceptionServices;
using EventApi.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace EventApi.Presentation.Middleware;

public sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger,
    IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(exception, "Cannot write problem details because the response has already started.");
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        var statusCode = GetStatusCode(exception);

        logger.LogError(exception, "Unhandled exception while processing request {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = GetDetail(exception, context.RequestServices.GetRequiredService<IHostEnvironment>()),
            Instance = context.Request.Path
        };

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        NoAvailableSeatsException => StatusCodes.Status409Conflict,
        ActiveBookingLimitExceededException => StatusCodes.Status409Conflict,
        EventAlreadyStartedException => StatusCodes.Status400BadRequest,
        ForbiddenOperationException => StatusCodes.Status403Forbidden,
        ValidationException => StatusCodes.Status400BadRequest,
        ArgumentException => StatusCodes.Status400BadRequest,
        InvalidOperationException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetDetail(Exception exception, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return exception.Message;

        return "An unexpected error occurred.";
    }
}

