using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Refunds;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Refunds.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest/refund-requests")]
    public sealed class TenantGuestRefundRequestsController(
        ISender sender,
        ICorrelationIdAccessor correlationIdAccessor)
        : ControllerBase
    {
        [HttpGet("{publicReference}")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<GuestRefundRequestStatus>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<GuestRefundRequestStatus>> Get(
            [FromRoute] string publicReference,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetGuestRefundRequestStatusQuery(publicReference), cancellationToken);
            if (result.IsFailure && result.Error!.Code == RefundRequestErrors.NotFoundCode)
            {
                await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                return new EmptyResult();
            }

            return result.IsSuccess
                ? Ok(result.Value)
                : result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
