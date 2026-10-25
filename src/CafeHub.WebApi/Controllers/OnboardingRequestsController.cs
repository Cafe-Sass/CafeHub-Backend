using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Commands;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            CancellationToken cancellationToken)
        {
            var command = new PayOnboardingRequestCommand(id);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value!);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
