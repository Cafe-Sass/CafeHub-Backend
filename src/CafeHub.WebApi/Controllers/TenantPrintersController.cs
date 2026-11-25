using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Commands;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Application.Printing.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/printers")]
    public sealed class TenantPrintersController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<IReadOnlyList<PrinterResponse>>> List(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ListPrintersQuery(branchId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrinterResponse>> Register(
            [FromRoute] string slug,
            Guid branchId,
            [FromBody] RegisterPrinterRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RegisterPrinterCommand(
                    branchId,
                    request.StationId,
                    request.Name,
                    ParseProfile(request.Profile),
                    request.ConnectionKind),
                cancellationToken);

            return result.IsSuccess
                ? CreatedAtAction(
                    nameof(List),
                    new { slug, branchId },
                    result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{printerId:guid}")]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrinterResponse>> Update(
            Guid branchId,
            Guid printerId,
            [FromBody] UpdatePrinterRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new UpdatePrinterCommand(
                    printerId,
                    branchId,
                    request.StationId,
                    request.Name,
                    ParseProfile(request.Profile),
                    request.ConnectionKind,
                    request.ConcurrencyToken!),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{printerId:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public Task<ActionResult<PrinterResponse>> Deactivate(
            Guid branchId,
            Guid printerId,
            [FromBody] PrinterLifecycleRequest request,
            CancellationToken cancellationToken) =>
            ChangeStatus(branchId, printerId, PrinterStatus.Inactive, request, cancellationToken);

        [HttpPost("{printerId:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public Task<ActionResult<PrinterResponse>> Reactivate(
            Guid branchId,
            Guid printerId,
            [FromBody] PrinterLifecycleRequest request,
            CancellationToken cancellationToken) =>
            ChangeStatus(branchId, printerId, PrinterStatus.Active, request, cancellationToken);

        internal static PrinterProfile ParseProfile(string? value) =>
            Enum.TryParse<PrinterProfile>(value, ignoreCase: true, out var profile)
                ? profile
                : (PrinterProfile)(-1);

        private async Task<ActionResult<PrinterResponse>> ChangeStatus(
            Guid branchId,
            Guid printerId,
            PrinterStatus status,
            PrinterLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new SetPrinterStatusCommand(printerId, branchId, status, request.ConcurrencyToken!),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
