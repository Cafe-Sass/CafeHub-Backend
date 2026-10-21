using System.Diagnostics;
using CafeHub.Application.Abstractions.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Errors
{
    /// <summary>
    /// Turns a failed <see cref="Result"/> into an MVC <see cref="ObjectResult"/> carrying the mapped
    /// ProblemDetails, so every controller reports failures through this helper and never builds error JSON.
    /// </summary>
    public static class ResultProblemDetailsExtensions
    {
        public static ObjectResult ToProblemResult(this Result result, HttpContext httpContext)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (result.IsSuccess)
            {
                throw new InvalidOperationException("A successful result cannot be mapped to a problem result.");
            }

            var traceIdentifier = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            var problemDetails = ErrorProblemDetailsMapper.Map(result.Error!, traceIdentifier);

            return new ObjectResult(problemDetails)
            {
                StatusCode = problemDetails.Status,
            };
        }
    }
}
