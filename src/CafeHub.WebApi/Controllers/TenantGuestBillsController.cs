using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.DTOs;
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
