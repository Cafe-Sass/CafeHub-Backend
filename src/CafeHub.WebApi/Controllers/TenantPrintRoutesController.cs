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
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/print-routes")]
    public sealed class TenantPrintRoutesController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<IReadOnlyList<PrintRouteResponse>>> List(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ListPrintRoutesQuery(branchId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrintRouteResponse>> Set(
            Guid branchId,
            [FromBody] SetPrintRouteRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new SetPrintRouteCommand(
                    branchId,
                    request.StationId,
                    ParseDocumentType(request.DocumentType),
                    request.PrinterId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        internal static PrintDocumentType ParseDocumentType(string? value) =>
            Enum.TryParse<PrintDocumentType>(value, ignoreCase: true, out var documentType)
                ? documentType
                : (PrintDocumentType)(-1);
    }
}
