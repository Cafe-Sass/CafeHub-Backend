using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/menu/products/{productId:guid}/station-assignment")]
    [TenantAuthorize(TenantPermissions.MenuManage)]
    [ProducesResponseType<ProductStationAssignmentResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public sealed class TenantProductStationAssignmentsController(
        ISender sender, ICorrelationIdAccessor correlation) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<ProductStationAssignmentResponse>> Get(
            Guid branchId, Guid productId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetProductStationAssignmentQuery(productId, branchId), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlation);
        }

        [HttpPut]
        public async Task<ActionResult<ProductStationAssignmentResponse>> Set(
            Guid branchId, Guid productId, [FromBody] SetProductStationAssignmentRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new SetProductStationAssignmentCommand(
                productId, branchId, request.StationId, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlation);
        }
    }
}
