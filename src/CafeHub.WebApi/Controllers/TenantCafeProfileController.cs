using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.CafeProfile.Commands;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.CafeProfile.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/cafe-profile")]
    public sealed class TenantCafeProfileController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantCafeProfileController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpGet]
        [TenantAuthorize(TenantPolicies.TenantUser)]
        public async Task<ActionResult<CafeProfileResponse>> Get(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetCafeProfileQuery(), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut]
        [TenantAuthorize(TenantPolicies.TenantOwner)]
        public async Task<ActionResult<CafeProfileResponse>> Put(UpdateCafeProfileRequest request, CancellationToken cancellationToken)
        {
            var command = new UpdateCafeProfileCommand(
                request.CafeName,
                request.LogoMediaReference,
                request.PrimaryColor,
                request.SecondaryColor,
                request.AccentColor,
                request.ContactPhone,
                request.ContactEmail,
                request.ContactAddress,
                request.Timezone,
                request.ConcurrencyToken);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
