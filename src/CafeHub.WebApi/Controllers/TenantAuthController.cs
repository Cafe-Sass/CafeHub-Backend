using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity.Commands;
using CafeHub.Application.Identity.DTOs;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route(TenantRouteConstants.ApiV1 + "/auth")]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    public sealed class TenantAuthController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantAuthController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantTokenResponse>> Login([FromBody] LoginTenantUserCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            if (result.IsSuccess) return Ok(result.Value);
            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }

    public static class TenantRouteConstants
    {
        public const string ApiV1 = "t/{slug}/api/v1";
    }
}
