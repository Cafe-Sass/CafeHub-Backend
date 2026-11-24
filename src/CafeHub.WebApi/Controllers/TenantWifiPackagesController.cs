using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
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
    [Route("t/{slug}/api/v1/wifi/packages")]
    public sealed class TenantWifiPackagesController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<OffsetPage<WifiPackageListItem>>> List(
            [FromQuery] GetWifiPackagesQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{packageId:guid}")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiPackageResponse>> Get(
            Guid packageId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetWifiPackageQuery(packageId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiPackageResponse>> Create(
            [FromBody] CreateWifiPackageRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new CreateWifiPackageCommand(
                    request.Name,
                    request.DurationMinutes,
                    request.SpeedMbps,
                    ParseDataMode(request.DataMode),
                    request.QuotaMb,
                    request.Price,
                    request.SortOrder),
                cancellationToken);

            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { packageId = result.Value.Id }, result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{packageId:guid}")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiPackageResponse>> Update(
            Guid packageId,
            [FromBody] UpdateWifiPackageRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new UpdateWifiPackageCommand(
                    packageId,
                    request.Name,
                    request.DurationMinutes,
                    request.SpeedMbps,
                    ParseDataMode(request.DataMode),
                    request.QuotaMb,
                    request.Price,
                    request.SortOrder,
                    request.ConcurrencyToken!),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{packageId:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiPackageResponse>> Deactivate(
            Guid packageId,
            [FromBody] WifiPackageLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new DeactivateWifiPackageCommand(packageId, request.ConcurrencyToken!),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{packageId:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiPackageResponse>> Reactivate(
            Guid packageId,
            [FromBody] WifiPackageLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ReactivateWifiPackageCommand(packageId, request.ConcurrencyToken!),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        internal static WifiDataMode ParseDataMode(string? value) =>
            Enum.TryParse<WifiDataMode>(value, ignoreCase: true, out var mode)
                ? mode
                : (WifiDataMode)(-1);
    }
}
