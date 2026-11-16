using System.Diagnostics;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Tables;

namespace CafeHub.WebApi.Errors
{
    internal static class GuestQrProblem
    {
        internal static Task WriteNotFoundAsync(
            HttpContext context,
            ICorrelationIdAccessor? correlationIdAccessor,
            CancellationToken cancellationToken)
        {
            var problem = ErrorProblemDetailsMapper.Map(
                GuestQrErrors.NotFound(),
                Activity.Current?.Id ?? context.TraceIdentifier,
                correlationIdAccessor?.CorrelationId);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return context.Response.WriteAsJsonAsync(
                problem, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        }
    }
}
