using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/payment-settings")]
    public sealed class TenantPaymentSettingsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.PaymentsSettingsManage)]
        public async Task<ActionResult<PaymentSettingsResponse>> Get(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetPaymentSettingsQuery(), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPut]
        [TenantAuthorize(TenantPermissions.PaymentsSettingsManage)]
        public async Task<ActionResult<PaymentSettingsResponse>> Put(
            [FromBody] UpdatePaymentSettingsRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdatePaymentSettingsCommand(
                request.OnlineEnabled,
                request.MethodLabel,
                request.PaymentLink,
                request.PaymentAddress,
                request.DisplayName,
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
