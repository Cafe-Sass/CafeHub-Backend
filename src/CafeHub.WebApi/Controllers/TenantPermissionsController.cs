using CafeHub.Application.Identity;
using CafeHub.Application.Roles;
using CafeHub.WebApi.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1")]
    [TenantAuthorize(TenantPermissions.RolesManage)]
    public sealed class TenantPermissionsController : ControllerBase
    {
        [HttpGet("permissions")]
        public ActionResult<IReadOnlyList<string>> Catalog() => Ok(TenantPermissions.All);

        [HttpGet("role-presets")]
        public ActionResult<IReadOnlyList<TenantRolePreset>> Presets() => Ok(new TenantRolePreset[]
        {
            new("Manager", [TenantPermissions.StaffRead, TenantPermissions.StaffManage]),
            new("Captain", []),
            new("Cashier", []),
            new("Barista", []),
        });
    }
}
