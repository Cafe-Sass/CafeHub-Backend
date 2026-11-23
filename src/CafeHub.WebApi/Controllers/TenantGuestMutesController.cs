using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Guests.Commands;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Guests.DTOs;
using CafeHub.Application.Guests.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest-mutes")]
    public sealed class TenantGuestMutesController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public TenantGuestMutesController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [TenantAuthorize(TenantPermissions.GuestsMute)]
        [ProducesResponseType<GuestMuteResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<ActionResult<GuestMuteResponse>> Mute(
            [FromRoute] string slug,
            [FromBody] MuteGuestDeviceRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new MuteGuestDeviceCommand
                {
                    OrderId = request.OrderId,
                    Reason = request.Reason ?? string.Empty,
                    ExpiryHours = request.ExpiryHours,
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return Created(
                    $"/t/{slug}/api/v1/guest-mutes/{result.Value.Id}",
                    GuestMuteResponse.From(result.Value));
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{muteId:guid}/lift")]
        [TenantAuthorize(TenantPermissions.GuestsMute)]
        [ProducesResponseType<GuestMuteResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<ActionResult<GuestMuteResponse>> Lift(
            Guid muteId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new UnmuteGuestDeviceCommand { MuteId = muteId }, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(GuestMuteResponse.From(result.Value));
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet]
        [TenantAuthorize(TenantPermissions.GuestsMuteAudit)]
        [ProducesResponseType<OffsetPage<GuestMuteListItem>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<GuestMuteListItem>>> List(
            [FromQuery] GetGuestMutesQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("summary")]
        [TenantAuthorize(TenantPermissions.GuestsMuteAudit)]
        [ProducesResponseType<IReadOnlyList<GuestMuteSummaryEntry>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<ActionResult<IReadOnlyList<GuestMuteSummaryEntry>>> Summary(
            [FromQuery] GetGuestMuteSummaryQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
