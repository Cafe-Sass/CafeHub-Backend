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
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/wifi")]
    public sealed class TenantBranchWifiController(
        ISender sender,
        TableQrLinks links,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet("qr")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiQrLinkResponse>> GetQr(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetWifiQrQuery(branchId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(links.CreateWifi(result.Value));
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("qr/regenerate")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiQrLinkResponse>> RegenerateQr(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RegenerateWifiQrCommand(branchId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(links.CreateWifi(result.Value));
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

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
