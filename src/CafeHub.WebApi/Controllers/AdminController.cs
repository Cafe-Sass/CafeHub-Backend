using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Identity.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/admin")]
    public sealed class AdminController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public AdminController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpGet("me")]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<CurrentAdminResponse>> Me(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetCurrentAdminQuery(), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
