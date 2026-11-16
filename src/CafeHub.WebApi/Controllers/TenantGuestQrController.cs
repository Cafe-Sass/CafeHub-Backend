using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest/qr")]
    public sealed class TenantGuestQrController(ISender sender, ICorrelationIdAccessor correlationIdAccessor)
        : ControllerBase
    {
        [HttpGet("{**code}")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<GuestQrContext>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<GuestQrContext>> Resolve(
            [FromRoute] string slug,
            [FromRoute] string? code,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ResolveGuestQrQuery(code), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
            return new EmptyResult();
        }
    }
}
