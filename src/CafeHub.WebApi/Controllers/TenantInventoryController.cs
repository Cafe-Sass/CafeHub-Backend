using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Inventory.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/inventory")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantInventoryController(
        ISender sender,
        ICorrelationIdAccessor correlationIdAccessor) : ControllerBase
    {
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";

        [HttpPost]
        [TenantAuthorize(TenantPermissions.InventoryManage)]
        [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<InventoryItemResponse>> Create(
            [FromRoute] string slug,
            [FromBody] CreateInventoryItemRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<InventoryUnit>(request.Unit, true, out var unit))
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.InvalidUnit())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new CreateInventoryItemCommand(
                    request.Name,
                    unit,
                    request.LowStockThreshold,
                    request.TracksExpiry,
                    request.NearExpiryDays),
                cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { slug, id = result.Value.Id }, result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPermissions.InventoryView)]
        [ProducesResponseType<OffsetPage<InventoryItemResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<InventoryItemResponse>>> List(
            [FromRoute] string slug,
            [FromQuery] ListInventoryItemsQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{id:guid}")]
        [TenantAuthorize(TenantPermissions.InventoryView)]
        [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<InventoryItemResponse>> Get(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetInventoryItemQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut("{id:guid}")]
        [TenantAuthorize(TenantPermissions.InventoryManage)]
        [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<InventoryItemResponse>> Update(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] UpdateInventoryItemRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<InventoryUnit>(request.Unit, true, out var unit))
            {
                return Result.Failure<InventoryItemResponse>(InventoryItemErrors.InvalidUnit())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new UpdateInventoryItemCommand(
                    id,
                    request.Name,
                    unit,
                    request.LowStockThreshold,
                    request.TracksExpiry,
                    request.NearExpiryDays,
                    request.ConcurrencyToken),
                cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/deactivate")]
        [TenantAuthorize(TenantPermissions.InventoryManage)]
        [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<InventoryItemResponse>> Deactivate(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] InventoryItemLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new DeactivateInventoryItemCommand(id, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id:guid}/reactivate")]
        [TenantAuthorize(TenantPermissions.InventoryManage)]
        [ProducesResponseType<InventoryItemResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<InventoryItemResponse>> Reactivate(
            [FromRoute] string slug,
            [FromRoute] Guid id,
            [FromBody] InventoryItemLifecycleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ReactivateInventoryItemCommand(id, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("branches/{branchId:guid}/batches")]
        [TenantAuthorize(TenantPermissions.InventoryReceive)]
        [ProducesResponseType<InventoryBatchResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<InventoryBatchResponse>> ReceiveBatch(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromBody] ReceiveStockRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ReceiveStockCommand
                {
                    BranchId = branchId,
                    ItemId = request.ItemId,
                    ReceivedQuantity = request.ReceivedQuantity,
                    ReceivedDate = request.ReceivedDate,
                    ExpiryDate = request.ExpiryDate,
                    IdempotencyKey = idempotencyKey,
                },
                cancellationToken);
            return result.IsSuccess
                ? StatusCode(StatusCodes.Status201Created, result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("branches/{branchId:guid}/batches")]
        [TenantAuthorize(TenantPermissions.InventoryView)]
        [ProducesResponseType<OffsetPage<InventoryBatchResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<InventoryBatchResponse>>> ListBatches(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromQuery] ListInventoryBatchesQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request with { BranchId = branchId }, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("stock")]
        [TenantAuthorize(TenantPermissions.InventoryView)]
        [ProducesResponseType<OffsetPage<StockLevelResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<StockLevelResponse>>> Stock(
            [FromRoute] string slug,
            [FromQuery] ListStockLevelsQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("movements")]
        [TenantAuthorize(TenantPermissions.InventoryView)]
        [ProducesResponseType<OffsetPage<StockMovementResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<StockMovementResponse>>> ListMovements(
            [FromRoute] string slug,
            [FromQuery] ListStockMovementsQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
