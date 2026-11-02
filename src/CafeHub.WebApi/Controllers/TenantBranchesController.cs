using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Branches.Queries;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches")]
    public sealed class TenantBranchesController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantBranchesController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpGet]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<OffsetPage<BranchResponse>>> List(
            [FromQuery] ListBranchesQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{branchId:guid}")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<BranchResponse>> Get(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetBranchQuery(branchId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
