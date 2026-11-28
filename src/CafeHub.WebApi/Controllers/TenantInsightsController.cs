using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Insights.DTOs;
using CafeHub.Application.Insights.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/insights")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantInsightsController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null) : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.InsightsView)]
        [ProducesResponseType<InsightsResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<ActionResult<InsightsResponse>> Get(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetInsightsQuery
                {
                    From = from ?? default,
                    To = to ?? default,
                    BranchId = branchId,
                },
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
