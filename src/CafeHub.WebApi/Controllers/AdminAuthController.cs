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
    [Route("api/v1/admin/auth")]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    public sealed class AdminAuthController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public AdminAuthController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AdminTokenResponse>> Login([FromBody] LoginAdminCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AdminTokenResponse>> Refresh([FromBody] RefreshAdminTokenCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout([FromBody] LogoutAdminCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return NoContent();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
