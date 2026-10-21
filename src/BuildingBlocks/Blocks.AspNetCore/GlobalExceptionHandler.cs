using Blocks.Domain;
using Blocks.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blocks.AspNetCore;

/// <summary>The one place where exceptions become HTTP responses (RFC 9457 problem details).</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var statusCode = exception switch
        {
            HttpException httpException => httpException.StatusCode,
            ValidationException or DomainException or ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        // an HttpException is thrown on purpose with a message written for the client (e.g. 503 "cannot verify");
        // anything else reaching 5xx is a bug or an outage
        var isUnexpected = statusCode >= StatusCodes.Status500InternalServerError && exception is not HttpException;
        if (isUnexpected)
            logger.LogError(exception, "Unhandled exception");

        var problem = new ProblemDetails
        {
            Status = statusCode,
            // insight - 5xx messages can leak internals (SQL, paths); clients get a generic title, the trace has the rest
            Title = isUnexpected ? "An unexpected error occurred." : exception.Message
        };

        if (exception is ValidationException validationException)
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
