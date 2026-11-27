using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Refunds;
using CafeHub.Application.Refunds.Commands;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Refunds.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest/bills")]
    public sealed class TenantGuestBillsController(ISender sender, ICorrelationIdAccessor correlationIdAccessor)
        : ControllerBase
    {
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";

        [HttpGet("{billReference}/payments")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<IReadOnlyList<GuestRefundablePayment>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<IReadOnlyList<GuestRefundablePayment>>> GetConfirmedPayments(
            [FromRoute] string billReference,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetGuestBillPaymentsQuery(billReference), cancellationToken);
            if (result.IsFailure && result.Error!.Code == RefundRequestErrors.NotFoundCode)
            {
                await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                return new EmptyResult();
            }

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{billReference}/refund-requests")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType<SubmitGuestRefundRequestResult>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<SubmitGuestRefundRequestResult>> SubmitRefundRequest(
            [FromRoute] string slug,
            [FromRoute] string billReference,
            [FromForm] SubmitGuestRefundRequestRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var streams = new List<Stream>();
            try
            {
                foreach (var file in request.Evidence ?? [])
                {
                    streams.Add(file.OpenReadStream());
                }

                var command = new SubmitGuestRefundRequestCommand
                {
                    BillReference = billReference,
                    PaymentId = request.PaymentId,
                    ClaimedAmount = request.ClaimedAmount,
                    Reason = request.Reason,
                    Details = request.Details,
                    GuestDisplayName = request.GuestDisplayName,
                    Evidence = streams.Select(stream => new RefundRequestEvidence(stream)).ToArray(),
                    IdempotencyKey = idempotencyKey,
                };
                var result = await sender.Send(command, cancellationToken);
                if (result.IsFailure && result.Error!.Code == RefundRequestErrors.NotFoundCode)
                {
                    await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                    return new EmptyResult();
                }

                if (result.IsFailure)
                {
                    return result.ToProblemResult(HttpContext, correlationIdAccessor);
                }

                var location = $"/t/{slug}/api/v1/guest/refund-requests/{result.Value.PublicReference}";
                return Created(location, result.Value);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    stream.Dispose();
                }
            }
        }

        [HttpPost("{billReference}/payment-proofs")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<SubmitPaymentProofResult>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<SubmitPaymentProofResult>> SubmitProof(
            [FromRoute] string slug,
            [FromRoute] string billReference,
            [FromForm] SubmitPaymentProofRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            using var content = request.Image?.OpenReadStream();
            var command = new SubmitPaymentProofCommand
            {
                BillReference = billReference,
                ClaimedAmount = request.ClaimedAmount,
                Note = request.Note,
                Content = content,
                IdempotencyKey = idempotencyKey,
            };

            var result = await sender.Send(command, cancellationToken);
            if (result.IsFailure && result.Error!.Code == BillingErrors.NotFoundCode)
            {
                await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                return new EmptyResult();
            }

            if (result.IsSuccess)
            {
                return Created(string.Empty, result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
