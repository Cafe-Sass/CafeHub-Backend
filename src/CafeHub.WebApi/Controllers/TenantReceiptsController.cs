using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
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
    [Route("t/{slug}/api/v1/receipts")]
    public sealed class TenantReceiptsController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet("{receiptId:guid}")]
        [TenantAuthorize(TenantPermissions.BillsView)]
        public async Task<ActionResult<ReceiptDetail>> GetById(
            Guid receiptId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetReceiptQuery(receiptId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
