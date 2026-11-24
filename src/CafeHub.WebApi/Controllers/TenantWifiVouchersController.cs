using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Commands;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Application.Wifi.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/wifi/packages/{packageId:guid}/vouchers")]
    public sealed class TenantWifiVouchersController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpPost("import")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiVoucherImportResult>> Import(
            Guid packageId,
            [FromBody] ImportWifiVouchersRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ImportWifiVouchersCommand(packageId, request.Codes),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("summary")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult<WifiVoucherSummary>> Summary(
            Guid packageId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetWifiVoucherSummaryQuery(packageId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("void")]
        [TenantAuthorize(TenantPermissions.WifiManage)]
        public async Task<ActionResult> Void(
            Guid packageId,
            [FromBody] VoidWifiVoucherRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new VoidWifiVoucherCommand(packageId, request.Code),
                cancellationToken);

            if (result.IsSuccess)
            {
                return NoContent();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
