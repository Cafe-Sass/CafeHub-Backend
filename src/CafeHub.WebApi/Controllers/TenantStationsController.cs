using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations.Commands;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Application.Stations.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/stations")]
    public sealed class TenantStationsController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantStationsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.StationsManage)]
        public async Task<ActionResult<StationResponse>> Create(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromBody] CreateStationRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateStationCommand(branchId, request.Name, request.SortOrder);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return CreatedAtAction(
                    nameof(Get),
                    new { slug, branchId, stationId = result.Value.Id },
                    result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<OffsetPage<StationResponse>>> List(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromQuery] ListStationsQuery query,
            CancellationToken cancellationToken)
        {
            var listQuery = query with { BranchId = branchId };
            var result = await sender.Send(listQuery, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{stationId:guid}")]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<StationResponse>> Get(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid stationId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetStationQuery(stationId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{stationId:guid}")]
        [TenantAuthorize(TenantPermissions.StationsManage)]
        public async Task<ActionResult<StationResponse>> Put(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid stationId,
            [FromBody] UpdateStationRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateStationCommand(stationId, request.Name, request.SortOrder, request.ConcurrencyToken);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{stationId:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.StationsManage)]
        public async Task<ActionResult<StationResponse>> Deactivate(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid stationId,
            [FromBody] StationLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DeactivateStationCommand(stationId, request.ConcurrencyToken);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{stationId:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.StationsManage)]
        public async Task<ActionResult<StationResponse>> Reactivate(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromRoute] Guid stationId,
            [FromBody] StationLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ReactivateStationCommand(stationId, request.ConcurrencyToken);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
