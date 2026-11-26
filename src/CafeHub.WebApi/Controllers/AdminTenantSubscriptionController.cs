using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Application.Subscriptions.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/admin/tenants/{tenantId:guid}/subscription")]
    [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class AdminTenantSubscriptionController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<SubscriptionOverviewResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<SubscriptionOverviewResponse>> Get(
            [FromRoute] Guid tenantId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetSubscriptionOverviewQuery(tenantId), cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("history")]
        [ProducesResponseType<OffsetPage<SubscriptionHistoryItem>>(
            StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<SubscriptionHistoryItem>>> History(
            [FromRoute] Guid tenantId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] bool includeTotalCount,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetSubscriptionHistoryQuery(tenantId, page ?? 1, pageSize, includeTotalCount),
                cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
