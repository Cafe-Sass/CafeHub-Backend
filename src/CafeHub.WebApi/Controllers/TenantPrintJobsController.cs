using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Commands;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Application.Printing.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/print-jobs")]
    public sealed class TenantPrintJobsController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<IReadOnlyList<PrintJobResponse>>> List(
            Guid branchId,
            [FromQuery] string? status,
            [FromQuery] Guid? printerId,
            [FromQuery] string? documentType,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ListPrintJobsQuery(branchId, status, printerId, documentType),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{jobId:guid}/cancel")]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrintJobResponse>> Cancel(
            Guid branchId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new CancelPrintJobCommand(jobId, branchId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{jobId:guid}/requeue")]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrintJobResponse>> Requeue(
            Guid branchId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RequeuePrintJobCommand(jobId, branchId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
