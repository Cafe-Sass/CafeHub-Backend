using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/bills")]
    public sealed class TenantBillsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";

        [HttpGet]
        [TenantAuthorize(TenantPermissions.BillsView)]
        public async Task<ActionResult<OffsetPage<BillListItem>>> List(
            [FromQuery] GetBillsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{billId:guid}")]
        [TenantAuthorize(TenantPermissions.BillsView)]
        public async Task<ActionResult<BillDetail>> GetById(
            Guid billId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetBillQuery(billId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{billId:guid}/payments/cash")]
        [TenantAuthorize(TenantPermissions.PaymentsCashConfirm)]
        public async Task<ActionResult<ConfirmCashPaymentResult>> ConfirmCash(
            Guid billId,
            [FromBody] ConfirmCashPaymentRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var command = new ConfirmCashPaymentCommand
            {
                BillId = billId,
                OrderId = request.OrderId,
                Amount = request.Amount,
                IdempotencyKey = idempotencyKey,
            };

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{billId:guid}/receipt")]
        [TenantAuthorize(TenantPermissions.BillsView)]
        public async Task<ActionResult<ReceiptDetail>> GetReceipt(
            Guid billId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetBillReceiptQuery(billId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{billId:guid}/close")]
        [TenantAuthorize(TenantPermissions.BillsClose)]
        public async Task<ActionResult<BillDetail>> Close(
            Guid billId,
            [FromBody] CloseBillRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CloseBillCommand
            {
                BillId = billId,
                Reason = request.Reason,
            };

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
