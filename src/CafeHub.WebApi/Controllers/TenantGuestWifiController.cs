using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tables;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Application.Wifi.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest/wifi")]
    public sealed class TenantGuestWifiController(
        ISender sender,
        ICorrelationIdAccessor correlationIdAccessor)
        : ControllerBase
    {
        [HttpGet("{code}")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<GuestWifiInfo>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<GuestWifiInfo>> Get(
            [FromRoute] string? code,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetGuestWifiQuery(code), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            if (result.Error!.Code == GuestQrErrors.NotFoundCode)
            {
                await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                return new EmptyResult();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
