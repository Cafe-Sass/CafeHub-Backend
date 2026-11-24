using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/guest/receipts")]
    public sealed class TenantGuestReceiptsController(
        ISender sender,
        ICorrelationIdAccessor correlationIdAccessor)
        : ControllerBase
    {
        [HttpGet("{billReference}")]
        [AllowAnonymous]
        [TableQrEndpoint(IsGuest = true)]
        [ProducesResponseType<ReceiptDetail>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        public async Task<ActionResult<ReceiptDetail>> Get(
            [FromRoute] string? billReference,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetGuestReceiptQuery(billReference), cancellationToken);
            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            if (result.Error!.Code == ReceiptErrors.NotFoundCode)
            {
                await GuestQrProblem.WriteNotFoundAsync(HttpContext, correlationIdAccessor, cancellationToken);
                return new EmptyResult();
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
