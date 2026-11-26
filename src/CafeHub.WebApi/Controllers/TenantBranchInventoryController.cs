using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/branches/{branchId:guid}/inventory")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public sealed class TenantBranchInventoryController(
        ISender sender,
        ICorrelationIdAccessor correlationIdAccessor) : ControllerBase
    {
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";

        [HttpPost("movements")]
        [TenantAuthorize(TenantPermissions.InventoryMove)]
        [ProducesResponseType<IReadOnlyList<StockMovementResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<IReadOnlyList<StockMovementResponse>>> RecordMovement(
            [FromRoute] string slug,
            [FromRoute] Guid branchId,
            [FromBody] RecordStockMovementRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<StockMovementType>(request.Type, true, out var type)
                || !StockMovement.IsManualType(type))
            {
                return Result.Failure<IReadOnlyList<StockMovementResponse>>(StockMovementErrors.InvalidType())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(
                new RecordStockMovementCommand
                {
                    BranchId = branchId,
                    ItemId = request.ItemId,
                    BatchId = request.BatchId,
                    Type = type,
                    Quantity = request.Quantity,
                    Reason = request.Reason,
                    IdempotencyKey = idempotencyKey,
                },
                cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
