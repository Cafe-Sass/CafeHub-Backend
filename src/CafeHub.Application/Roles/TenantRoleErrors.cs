using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Roles
{
    public static class TenantRoleErrors
    {
        public static Error NotFound() => Error.NotFound("Roles.NotFound", "The role was not found.");

        public static Error NameConflict() => Error.Conflict("Roles.NameConflict", "The role name is already reserved.");

        public static Error InUse() => Error.Conflict("Roles.InUse", "Remove all memberships before retiring the role.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict("Roles.ConcurrencyConflict", "The role changed. Reload and try again.");
    }
}
