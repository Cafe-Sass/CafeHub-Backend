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
    [Route("t/{slug}/api/v1/payments")]
    public sealed class TenantPaymentsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.PaymentsOnlineReview)]
        public async Task<ActionResult<OffsetPage<PaymentListItem>>> List(
            [FromQuery] GetPaymentsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{paymentId:guid}/proof")]
        [TenantAuthorize(TenantPermissions.PaymentsOnlineReview)]
        public async Task<ActionResult> Proof(Guid paymentId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPaymentProofQuery(paymentId), cancellationToken);

            if (result.IsFailure)
            {
                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(result.Value.Content, result.Value.ContentType);
        }

        [HttpPost("{paymentId:guid}/confirm")]
        [TenantAuthorize(TenantPermissions.PaymentsOnlineReview)]
        public async Task<ActionResult<ConfirmPaymentResult>> Confirm(
            Guid paymentId,
            [FromBody] ConfirmPaymentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ConfirmPaymentCommand
                {
                    PaymentId = paymentId,
                    Amount = request.Amount,
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{paymentId:guid}/reject")]
        [TenantAuthorize(TenantPermissions.PaymentsOnlineReview)]
        public async Task<ActionResult<RejectPaymentResult>> Reject(
            Guid paymentId,
            [FromBody] RejectPaymentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RejectPaymentCommand
                {
                    PaymentId = paymentId,
                    Reason = request.Reason,
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
