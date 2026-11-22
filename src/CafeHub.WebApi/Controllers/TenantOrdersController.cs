using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Orders.Queries;
using CafeHub.Application.Pagination;
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
    }
}
