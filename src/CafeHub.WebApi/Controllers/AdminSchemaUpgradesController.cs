using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Upgrades.Commands;
using CafeHub.Application.Upgrades.DTOs;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("api/v1/admin/tenants/{tenantId:guid}/schema-upgrades")]
    public sealed class AdminSchemaUpgradesController : ControllerBase
    {
        private readonly ISender sender;
        private readonly ICorrelationIdAccessor? correlationIdAccessor;

        public AdminSchemaUpgradesController(
            ISender sender,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.correlationIdAccessor = correlationIdAccessor;
        }

        [HttpPost]
        [Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
        public async Task<ActionResult<TenantSchemaUpgradeResponse>> Queue(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new QueueTenantSchemaUpgradeCommand(tenantId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }
    }
}
