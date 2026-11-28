using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Discovery;
using CafeHub.Application.Discovery.Queries;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

        [HttpGet("{code}/menu")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<OffsetPage<GuestMenuItemResponse>>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<OffsetPage<GuestMenuItemResponse>>> Menu(
            [FromRoute] string slug,
            [FromRoute] string? code,
            [FromQuery] GetGuestMenuQuery request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request with { Code = code }, cancellationToken);
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

        [HttpPost("{code}/discovery")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [EnableRateLimiting(DiscoveryRateLimiting.PolicyName)]
        [ProducesResponseType<ProductDiscoveryResult>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<ProductDiscoveryResult>> Discovery(
            [FromRoute] string slug,
            [FromRoute] string? code,
            [FromBody] ProductDiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetProductDiscoveryQuery { Code = code, Query = request.Query },
                cancellationToken);
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

        [HttpGet("{code}/payment-options")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<GuestPaymentOptionsResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<GuestPaymentOptionsResponse>> PaymentOptions(
            [FromRoute] string slug,
            [FromRoute] string? code,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetGuestPaymentOptionsQuery(code), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
            return new EmptyResult();
        }
    }
}
