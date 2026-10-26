using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/admin/onboarding-requests")]
    public sealed class AdminOnboardingRequestsController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public AdminOnboardingRequestsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpGet]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<OffsetPage<OnboardingRequestListItem>>> List(
            [FromQuery] ListOnboardingRequestsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<OnboardingRequestDetail>> Get(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetOnboardingRequestQuery(id), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/approve")]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<ApprovalResponse>> Approve(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ApproveOnboardingRequestCommand(id), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<RejectionResponse>> Reject(
            Guid id,
            [FromBody] RejectOnboardingRequestCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command with { RequestId = id }, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
