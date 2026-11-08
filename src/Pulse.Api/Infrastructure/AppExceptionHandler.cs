using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pulse.Application.Common;

namespace Pulse.Api.Infrastructure;

/// <summary>Turns expected failures into problem+json with a stable <c>code</c> clients can switch on.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problems, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        switch (exception)
        {
            case AppException app:
                problem = new ProblemDetails { Status = app.Status, Title = app.Message, Detail = app.Message };
                problem.Extensions["code"] = app.Code;
                if (app.Errors is not null)
                {
                    problem.Extensions["errors"] = app.Errors;
                }

                break;

            case BadHttpRequestException bad:
                problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "The request could not be read.", Detail = bad.Message };
                problem.Extensions["code"] = "bad_request";
                break;

            default:
                return false;
        }

        if (problem.Status >= 500)
        {
            logger.LogError(exception, "Request failed");
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }
}
