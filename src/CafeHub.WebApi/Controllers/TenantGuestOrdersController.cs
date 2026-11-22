using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Tables;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest/orders")]
    public sealed class TenantGuestOrdersController(ISender sender, ICorrelationIdAccessor correlationIdAccessor)
        : ControllerBase
    {
        private const string IdempotencyKeyHeaderName = "Idempotency-Key";
        private const string GuestDeviceHeaderName = "X-Guest-Device";

        [HttpPost]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<GuestOrderResponse>(StatusCodes.Status201Created, "application/json")]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<GuestOrderResponse>> Submit(
            [FromRoute] string slug,
            [FromBody] SubmitGuestOrderRequest request,
            [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
            [FromHeader(Name = GuestDeviceHeaderName)] string? guestDevice,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<OrderServiceType>(request.ServiceType, true, out var serviceType)
                || !Enum.IsDefined(serviceType))
            {
                return Result.Failure<GuestOrderResponse>(OrdersErrors.InvalidServiceType())
                    .ToProblemResult(HttpContext, correlationIdAccessor);
            }

            var command = new SubmitOrderCommand
            {
                IdempotencyKey = idempotencyKey,
                Code = request.Code,
                ServiceType = serviceType,
                Source = OrderSource.Guest,
                GuestDisplayName = request.GuestDisplayName ?? string.Empty,
                GuestDeviceId = guestDevice is not null && Order.IsValidGuestDeviceId(guestDevice) ? guestDevice : null,
                ExpectedTotal = request.ExpectedTotal,
                Lines = request.Lines?
                    .Select(line => new SubmitOrderLine(
                        line.ProductId,
                        line.SizeId,
                        line.AddonIds ?? [],
                        line.Quantity,
                        line.ExpectedUnitPrice,
                        line.ExpectedAddonPrices ?? []))
                    .ToArray() ?? [],
            };

            var result = await sender.Send(command, cancellationToken);
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
            return Created($"/t/{slug}/api/v1/guest/orders/{result.Value.PublicReference}", response);
        }
    }
}
