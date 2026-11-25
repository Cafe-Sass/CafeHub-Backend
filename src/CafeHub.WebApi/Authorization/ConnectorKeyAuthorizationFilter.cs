using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Contracts;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CafeHub.WebApi.Authorization
{
    /// <summary>
    /// Rate limits every connector request per presented key, authenticates it from its key header, and
    /// exposes the connector to the action. A missing, malformed, unknown, revoked, or mismatched key is the
    /// same 401 problem, and the key is never echoed or logged.
    /// </summary>
    public sealed class ConnectorKeyAuthorizationFilter(
        PrintConnectorAuthenticator authenticator,
        ConnectorRateLimiter rateLimiter,
        ICorrelationIdAccessor correlationIdAccessor) : IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-Connector-Key";

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var lease = rateLimiter.Acquire(ConnectorRateLimiting.GetPartitionKey(context.HttpContext));
            if (!lease.IsAcquired)
            {
                var rejection = ConnectorRateLimiting.CreateRejection(context.HttpContext, lease);
                lease.Dispose();
                context.Result = rejection;
                return;
            }

            context.HttpContext.Response.RegisterForDispose(lease);
            var presentedKey = context.HttpContext.Request.Headers[HeaderName].ToString();
            var result = await authenticator.AuthenticateAsync(
                presentedKey,
                context.HttpContext.RequestAborted);
            if (result.IsFailure)
            {
                context.Result = Result.Failure(result.Error!).ToProblemResult(
                    context.HttpContext,
                    correlationIdAccessor);
                return;
            }

            context.HttpContext.SetPrintConnector(result.Value);
        }
    }

    internal static class ConnectorHttpContextItems
    {
        private const string ItemKey = "CafeHub.PrintConnector";

        internal static void SetPrintConnector(this HttpContext context, PrintConnectorAccess connector) =>
            context.Items[ItemKey] = connector;

        internal static PrintConnectorAccess GetPrintConnector(this HttpContext context) =>
            context.Items[ItemKey] as PrintConnectorAccess
            ?? throw new InvalidOperationException("The connector key was not authenticated.");
    }
}
