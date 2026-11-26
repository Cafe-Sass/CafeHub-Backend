using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Subscriptions;
using CafeHub.Application.Subscriptions.Commands;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Application.Subscriptions.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/subscription")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantSubscriptionController(
        ISender sender,
        ICurrentTenantUser currentUser,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.SubscriptionManage)]
        [ProducesResponseType<SubscriptionOverviewResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<SubscriptionOverviewResponse>> Get(
            CancellationToken cancellationToken)
        {
            if (currentUser.TenantId is not { } tenantId)
            {
                return NotFoundProblem<SubscriptionOverviewResponse>();
            }

            var result = await sender.Send(
                new GetSubscriptionOverviewQuery(tenantId), cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("history")]
        [TenantAuthorize(TenantPermissions.SubscriptionManage)]
        [ProducesResponseType<OffsetPage<SubscriptionHistoryItem>>(
            StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<SubscriptionHistoryItem>>> History(
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] bool includeTotalCount,
            CancellationToken cancellationToken)
        {
            if (currentUser.TenantId is not { } tenantId)
            {
                return NotFoundProblem<OffsetPage<SubscriptionHistoryItem>>();
            }

            var result = await sender.Send(
                new GetSubscriptionHistoryQuery(tenantId, page ?? 1, pageSize, includeTotalCount),
                cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("renewal/{renewalId:guid}/approve")]
        [TenantAuthorize(TenantPermissions.SubscriptionManage)]
        [ProducesResponseType<SubscriptionRenewalResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<SubscriptionRenewalResponse>> Approve(
            [FromRoute] Guid renewalId,
            CancellationToken cancellationToken)
        {
            if (currentUser.TenantId is not { } tenantId)
            {
                return NotFoundProblem<SubscriptionRenewalResponse>();
            }

            var result = await sender.Send(
                new ApproveRenewalRequestCommand(
                    renewalId, tenantId, currentUser.UserId?.ToString()),
                cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("renewal/{renewalId:guid}/decline")]
        [TenantAuthorize(TenantPermissions.SubscriptionManage)]
        [ProducesResponseType<SubscriptionRenewalResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<SubscriptionRenewalResponse>> Decline(
            [FromRoute] Guid renewalId,
            CancellationToken cancellationToken)
        {
            if (currentUser.TenantId is not { } tenantId)
            {
                return NotFoundProblem<SubscriptionRenewalResponse>();
            }

            var result = await sender.Send(
                new DeclineRenewalRequestCommand(
                    renewalId, tenantId, currentUser.UserId?.ToString()),
                cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("next-plan")]
        [TenantAuthorize(TenantPermissions.SubscriptionManage)]
        [ProducesResponseType<SubscriptionRenewalResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<SubscriptionRenewalResponse>> SetNextPlan(
            [FromBody] SetNextPlanRequest request,
            CancellationToken cancellationToken)
        {
            if (currentUser.TenantId is not { } tenantId)
            {
                return NotFoundProblem<SubscriptionRenewalResponse>();
            }

            var result = await sender.Send(
                new SetOwnerNextPlanCommand(
                    tenantId, request.PlanVersionId, currentUser.UserId?.ToString()),
                cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        private ActionResult NotFoundProblem<TResponse>() =>
            Result.Failure<TResponse>(SubscriptionErrors.NotFound())
                .ToProblemResult(HttpContext, correlationIdAccessor);
    }

    public sealed record SetNextPlanRequest(Guid PlanVersionId);
}
