using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Pagination;
using CafeHub.Application.Plans.Commands;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Application.Plans.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/admin/plans")]
    public sealed class AdminPlansController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public AdminPlansController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<PlanResponse>> Create(
            [FromBody] CreatePlanCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return CreatedAtAction(nameof(Get), new { planId = result.Value.Id }, result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{planId:guid}/versions")]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<PlanResponse>> CreateVersion(
            Guid planId,
            [FromBody] CreatePlanVersionCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command with { PlanId = planId }, cancellationToken);

            if (result.IsSuccess)
            {
                return CreatedAtAction(nameof(Get), new { planId = result.Value.Id }, result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<OffsetPage<PlanResponse>>> List(
            [FromQuery] ListPlansQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{planId:guid}")]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<PlanResponse>> Get(Guid planId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPlanQuery(planId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
