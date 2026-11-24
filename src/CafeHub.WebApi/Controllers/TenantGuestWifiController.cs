using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.DTOs;
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
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";
        [HttpPost("{code}/orders")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<GuestOrderResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<GuestOrderResponse>> SubmitOrder(
            [FromRoute] string slug,
            [FromRoute] string? code,
            [FromBody] SubmitWifiOrderRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new SubmitWifiOrderCommand
                {
                    Code = code,
                    PackageId = request.PackageId,
                    GuestDisplayName = request.GuestDisplayName ?? string.Empty,
                    IdempotencyKey = idempotencyKey,
                },
                cancellationToken);
            if (result.IsFailure)
            {
                if (result.Error!.Code == GuestQrErrors.NotFoundCode)
                {
                    await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                    return new EmptyResult();
                }

                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var response = new GuestOrderResponse(
                result.Value.OrderNumber,
                result.Value.PublicReference,
                result.Value.Status,
                result.Value.TotalAmount,
                result.Value.ResponseDeadline);
            return Created(
                $"/t/{slug}/api/v1/guest/orders/{result.Value.PublicReference}",
                response);
        }

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
