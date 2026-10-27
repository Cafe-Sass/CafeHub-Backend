using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/onboarding/requests")]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    public sealed class OnboardingRequestsController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public OnboardingRequestsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<OwnerRegistrationResponse>> Post(
            RegisterOwnerCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                var response = result.Value!;
                return response.IsDuplicate
                    ? Ok(response)
                    : Created(string.Empty, response);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{id}/payments")]
        [AllowAnonymous]
        public async Task<ActionResult<PaymentOutcomeResponse>> Pay(
            Guid id,
            [FromHeader(Name = "Idempotency-Key"), BindRequired] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var command = new PayOnboardingRequestCommand(id)
            {
                IdempotencyKey = idempotencyKey,
            };
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value!);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{id}/status")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimiting.PolicyName)]
        public async Task<ActionResult<OnboardingStatusResponse>> GetStatus(
            string id,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(id, out var requestId))
            {
                return Result
                    .Failure(OnboardingErrors.RequestNotFound())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var result = await sender.Send(new GetOnboardingStatusQuery(requestId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value!);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
