using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Branches.Commands;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Branches.Queries;
using CafeHub.Application.Identity;
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
        [TenantAuthorize(TenantPermissions.BranchesRead)]
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
        [TenantAuthorize(TenantPermissions.BranchesRead)]
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

        [HttpPost]
        [TenantAuthorize(TenantPermissions.BranchesManage)]
        public async Task<ActionResult<BranchResponse>> Create(
            [FromRoute] string slug,
            [FromBody] CreateBranchRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateBranchCommand(
                request.Name,
                request.Address,
                request.ContactPhone,
                request.OpeningHoursSummary);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return CreatedAtAction(nameof(Get), new { slug, branchId = result.Value.Id }, result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{branchId:guid}")]
        [TenantAuthorize(TenantPermissions.BranchesManage)]
        public async Task<ActionResult<BranchResponse>> Put(
            Guid branchId,
            [FromBody] UpdateBranchRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateBranchCommand(
                branchId,
                request.Name,
                request.Address,
                request.ContactPhone,
                request.OpeningHoursSummary,
                request.ConcurrencyToken);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{branchId:guid}/suspend")]
        [TenantAuthorize(TenantPermissions.BranchesManage)]
        public async Task<ActionResult<BranchResponse>> Suspend(
            Guid branchId,
            [FromBody] BranchLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new SuspendBranchCommand(branchId, request.ConcurrencyToken),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{branchId:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.BranchesManage)]
        public async Task<ActionResult<BranchResponse>> Reactivate(
            Guid branchId,
            [FromBody] BranchLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ReactivateBranchCommand(branchId, request.ConcurrencyToken),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{branchId:guid}/retire")]
        [TenantAuthorize(TenantPermissions.BranchesManage)]
        public async Task<ActionResult<BranchResponse>> Retire(
            Guid branchId,
            [FromBody] BranchLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RetireBranchCommand(branchId, request.ConcurrencyToken),
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
