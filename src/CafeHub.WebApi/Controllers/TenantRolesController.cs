using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Application.Roles;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/roles")]
    [TenantAuthorize(TenantPermissions.RolesManage)]
    public sealed class TenantRolesController(ISender sender, ICorrelationIdAccessor correlation) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TenantRoleResponse>>> List(CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ListTenantRoles(), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlation);
        }

        [HttpGet("{roleId:guid}")]
        public async Task<ActionResult<TenantRoleResponse>> Get(Guid roleId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTenantRole(roleId), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlation);
        }

        [HttpPost]
        public async Task<ActionResult<TenantRoleResponse>> Create([FromRoute] string slug, CreateTenantRole request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request, cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { slug, roleId = result.Value.Id }, result.Value)
                : result.ToProblemResult(HttpContext, correlation);
        }

        [HttpPut("{roleId:guid}")]
        public async Task<ActionResult<TenantRoleResponse>> Update(Guid roleId, UpdateTenantRoleRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new UpdateTenantRole(roleId, request.Name, request.PermissionCodes, request.ConcurrencyToken), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult(HttpContext, correlation);
        }

        [HttpDelete("{roleId:guid}")]
        public async Task<ActionResult> Retire(Guid roleId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RetireTenantRole(roleId), cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblemResult(HttpContext, correlation);
        }
    }
}
