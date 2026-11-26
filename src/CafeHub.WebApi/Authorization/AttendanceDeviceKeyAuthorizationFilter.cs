using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CafeHub.WebApi.Authorization
{
    /// <summary>
    /// Rate limits every attendance device request per presented key, authenticates it from its key header,
    /// and exposes the device to the action. A missing, malformed, unknown, revoked, or mismatched key is the
    /// same 401 problem, and the key is never echoed or logged.
    /// </summary>
    public sealed class AttendanceDeviceKeyAuthorizationFilter(
        AttendanceDeviceAuthenticator authenticator,
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

            context.HttpContext.SetAttendanceDevice(result.Value);
        }
    }

    internal static class AttendanceDeviceHttpContextItems
    {
        private const string ItemKey = "CafeHub.AttendanceDevice";

        internal static void SetAttendanceDevice(this HttpContext context, AttendanceDeviceAccess device) =>
            context.Items[ItemKey] = device;

        internal static AttendanceDeviceAccess GetAttendanceDevice(this HttpContext context) =>
            context.Items[ItemKey] as AttendanceDeviceAccess
            ?? throw new InvalidOperationException("The attendance device key was not authenticated.");
    }
}
