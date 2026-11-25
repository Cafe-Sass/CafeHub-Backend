using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Printing.Commands;
using CafeHub.Application.Printing.DTOs;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/connector/print")]
    [AllowAnonymous]
    [ServiceFilter(typeof(ConnectorKeyAuthorizationFilter))]
    public sealed class TenantPrintConnectorController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpPost("claim")]
        public async Task<ActionResult<PrintJobResponse>> Claim(CancellationToken cancellationToken)
        {
            var connector = HttpContext.GetPrintConnector();
            var result = await sender.Send(
                new ClaimPrintJobCommand(connector.Id, connector.BranchId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            return result.Value is null ? NoContent() : Ok(result.Value);
        }

        [HttpPost("jobs/{jobId:guid}/succeeded")]
        public async Task<ActionResult<PrintJobResponse>> Succeeded(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            var connector = HttpContext.GetPrintConnector();
            var result = await sender.Send(
                new ReportPrintJobSucceededCommand(jobId, connector.BranchId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("jobs/{jobId:guid}/failed")]
        public async Task<ActionResult<PrintJobResponse>> Failed(
            Guid jobId,
            [FromBody] ConnectorFailureRequest request,
            CancellationToken cancellationToken)
        {
            var connector = HttpContext.GetPrintConnector();
            var result = await sender.Send(
                new ReportPrintJobFailedCommand(jobId, connector.BranchId, request.ErrorCode),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
