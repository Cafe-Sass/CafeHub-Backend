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
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/print-connectors")]
    public sealed class TenantPrintConnectorsController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<IReadOnlyList<PrintConnectorResponse>>> List(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ListPrintConnectorsQuery(branchId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrintConnectorCreatedResponse>> Create(
            [FromRoute] string slug,
            Guid branchId,
            [FromBody] CreatePrintConnectorRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new CreatePrintConnectorCommand(branchId, request.Name),
                cancellationToken);

            return result.IsSuccess
                ? CreatedAtAction(
                    nameof(List),
                    new { slug, branchId },
                    result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{connectorId:guid}/revoke")]
        [TenantAuthorize(TenantPermissions.PrintersManage)]
        public async Task<ActionResult<PrintConnectorResponse>> Revoke(
            Guid branchId,
            Guid connectorId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RevokePrintConnectorCommand(connectorId, branchId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
