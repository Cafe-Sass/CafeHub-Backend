using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Commands;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/wifi")]
    public sealed class TenantBranchWifiController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpPut("network")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult> SetNetwork(
            Guid branchId,
            [FromBody] SetBranchWifiNetworkRequest request,
            CancellationToken cancellationToken)
        {
            var command = new SetBranchWifiNetworkCommand(
                branchId,
                request.Ssid,
                ParseSecurity(request.Security),
                request.Password);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            return result.Value is null ? NoContent() : Ok(result.Value);
        }

        private static WifiSecurity? ParseSecurity(string? value) =>
            value is null
                ? null
                : Enum.TryParse<WifiSecurity>(value, ignoreCase: true, out var security)
                    ? security
                    : (WifiSecurity)(-1);
    }
}
