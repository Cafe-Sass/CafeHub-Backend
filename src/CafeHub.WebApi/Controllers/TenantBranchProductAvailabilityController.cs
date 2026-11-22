using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/menu/availability/products")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantBranchProductAvailabilityController(
        ISender sender, ICorrelationIdAccessor correlationIdAccessor) : ControllerBase
    {
        [HttpPut("{productId:guid}")]
        [TenantAuthorize(TenantPermissions.MenuAvailability)]
        [ProducesResponseType<BranchProductAvailabilityResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<BranchProductAvailabilityResponse>> Set(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid productId,
            [FromBody] SetBranchProductAvailabilityRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new SetBranchProductAvailabilityCommand(branchId, productId, request.Available, request.Reason),
                cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPermissions.MenuAvailability)]
        [ProducesResponseType<OffsetPage<BranchProductAvailabilityItem>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<BranchProductAvailabilityItem>>> List(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromQuery] ListBranchProductAvailabilityQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request with { BranchId = branchId }, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
