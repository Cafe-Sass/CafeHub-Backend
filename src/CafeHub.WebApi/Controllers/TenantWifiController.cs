using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Commands;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Application.Wifi.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/wifi")]
    public sealed class TenantWifiController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet("settings")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiSettingsResponse>> GetSettings(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetWifiSettingsQuery(), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("settings")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiSettingsResponse>> PutSettings(
            [FromBody] UpdateWifiSettingsRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateWifiSettingsCommand(
                ParsePolicy(request.Policy),
                request.ConcurrencyToken!);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        internal static WifiPolicy ParsePolicy(string? value) =>
            Enum.TryParse<WifiPolicy>(value, ignoreCase: true, out var policy)
                ? policy
                : (WifiPolicy)(-1);
    }
}
