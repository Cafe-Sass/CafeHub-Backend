using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Orders.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/orders")]
    public sealed class TenantOrdersController : ControllerBase
    {
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";

        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantOrdersController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpGet]
        [TenantAuthorize(TenantPermissions.OrdersView)]
        public async Task<ActionResult<OffsetPage<OrderFeedItem>>> Feed(
            [FromQuery] GetOrderFeedQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{orderId:guid}")]
        [TenantAuthorize(TenantPermissions.OrdersView)]
        public async Task<ActionResult<OrderFeedItem>> GetById(
            Guid orderId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetOrderQuery(orderId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{orderId:guid}/accept")]
        [TenantAuthorize(TenantPermissions.OrdersDecide)]
        public async Task<ActionResult<OrderDecisionResult>> Accept(
            Guid orderId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new AcceptOrderCommand(orderId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{orderId:guid}/reject")]
        [TenantAuthorize(TenantPermissions.OrdersDecide)]
        public async Task<ActionResult<OrderDecisionResult>> Reject(
            Guid orderId,
            [FromBody] RejectOrderRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RejectOrderCommand(orderId, request.Reason), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("assisted")]
        [TenantAuthorize(TenantPermissions.OrdersCaptain)]
        [ProducesResponseType<AssistedOrderResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        public async Task<ActionResult<AssistedOrderResponse>> SubmitAssisted(
            [FromRoute] string slug,
            [FromBody] SubmitAssistedOrderRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<OrderServiceType>(request.ServiceType, true, out var serviceType)
                || !Enum.IsDefined(serviceType))
            {
                return Result.Failure<AssistedOrderResponse>(OrdersErrors.InvalidServiceType())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var command = new SubmitAssistedOrderCommand
            {
                IdempotencyKey = idempotencyKey,
                TableId = request.TableId,
                ServiceType = serviceType,
                GuestDisplayName = request.GuestDisplayName ?? string.Empty,
                ExpectedTotal = request.ExpectedTotal,
                Lines = request.Lines?
                    .Select(line => new SubmitOrderLine(
                        line.ProductId,
                        line.SizeId,
                        line.AddonIds ?? [],
                        line.Quantity,
                        line.ExpectedUnitPrice,
                        line.ExpectedAddonPrices ?? []))
                    .ToArray() ?? [],
            };

            var result = await sender.Send(command, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            return Created(
                $"/t/{slug}/api/v1/orders",
                new AssistedOrderResponse(
                    result.Value.OrderNumber,
                    result.Value.PublicReference,
                    result.Value.Status,
                    result.Value.TotalAmount));
        }

        [HttpGet("missed-summary")]
        [TenantAuthorize(TenantPermissions.MissesView)]
        public async Task<ActionResult<OffsetPage<MissedResponseSummaryEntry>>> MissedSummary(
            [FromQuery] GetMissedResponsesSummaryQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
