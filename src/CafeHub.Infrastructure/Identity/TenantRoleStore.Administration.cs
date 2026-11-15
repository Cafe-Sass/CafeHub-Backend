using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Roles;
using CafeHub.Application.Staff;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed partial class TenantRoleStore
    {
        public async Task<Result<TenantRoleResponse>> CreateRoleAsync(
            string name, IReadOnlyList<string> permissions, Guid actorId, CancellationToken cancellationToken)
        {
            var normalized = name.ToLowerInvariant();
            if (await NameIsReservedAsync(normalized, null, cancellationToken))
            {
                return Result.Failure<TenantRoleResponse>(TenantRoleErrors.NameConflict());
            }

            var now = timeProvider.GetUtcNow();
            var role = TenantRole.Create(Guid.CreateVersion7(), name, normalized, Guid.NewGuid().ToString("N"), now, actorId);
            context.TenantRoles.Add(role);
            ReplaceRoleClaims(role.Id, [], permissions, now);

            var changes = TenantAuditChanges.Create([
                new("Name", null, name),
                new("Status", null, "Active"),
                new("PermissionCodes", null, FormatPermissions(permissions)),
            ]);
            return await SaveRoleAsync(role, permissions, "roles.created.succeeded", changes, actorId, cancellationToken);
        }

        public async Task<Result<TenantRoleResponse>> UpdateRoleAsync(
            Guid roleId, string name, IReadOnlyList<string> permissions, uint version, Guid actorId, CancellationToken cancellationToken)
        {
            await context.LockRoleAsync(roleId, cancellationToken);
            var role = await FindActiveRoleAsync(roleId, cancellationToken);
            if (role is null)
            {
                return Result.Failure<TenantRoleResponse>(TenantRoleErrors.NotFound());
            }

            if (role.IsOwnerRole)
            {
                return Result.Failure<TenantRoleResponse>(AuthErrors.Forbidden());
            }

            if (RoleVersion(role) != version)
            {
                return Result.Failure<TenantRoleResponse>(TenantRoleErrors.ConcurrencyConflict());
            }

            var normalized = name.ToLowerInvariant();
            if (await NameIsReservedAsync(normalized, roleId, cancellationToken))
            {
                return Result.Failure<TenantRoleResponse>(TenantRoleErrors.NameConflict());
            }

            var claims = await context.TenantRoleClaims
                .Where(claim => claim.RoleId == roleId && claim.ClaimType == TenantPermissions.ClaimType)
                .ToListAsync(cancellationToken);
            var before = FormatPermissions(claims.Select(claim => claim.ClaimValue!));
            var after = FormatPermissions(permissions);
            if (role.Name == name && before == after)
            {
                return Result.Success(ToResponse(role, permissions, version));
            }

            var changes = TenantAuditChanges.Update([new("Name", role.Name, name), new("PermissionCodes", before, after)]);
            var now = timeProvider.GetUtcNow();
            role.Revise(name, normalized, actorId, now);
            ReplaceRoleClaims(role.Id, claims, permissions, now);

            return await SaveRoleAsync(role, permissions, "roles.updated.succeeded", changes, actorId, cancellationToken);
        }

        public async Task<Result> RetireRoleAsync(Guid roleId, Guid actorId, CancellationToken cancellationToken)
        {
            await context.LockRoleAsync(roleId, cancellationToken);
            var role = await FindActiveRoleAsync(roleId, cancellationToken);
            if (role is null)
            {
                return Result.Failure(TenantRoleErrors.NotFound());
            }

            if (role.IsOwnerRole)
            {
                return Result.Failure(AuthErrors.Forbidden());
            }

            if (await context.TenantUserRoles.AnyAsync(link => link.RoleId == roleId, cancellationToken))
            {
                return Result.Failure(TenantRoleErrors.InUse());
            }

            role.Retire(actorId, timeProvider.GetUtcNow());
            return await AuditAsync("roles.retired.succeeded", "tenant_roles", roleId, actorId,
                TenantAuditChanges.Update([new("Status", "Active", "Retired")]), cancellationToken);
        }

        public async Task<IReadOnlyList<TenantRoleResponse>> ListRolesAsync(CancellationToken cancellationToken)
        {
            var roles = context.TenantRoles
                .Where(role => role.DeletedAt == null)
                .OrderBy(role => role.NormalizedName);
            var definitions = await ProjectRoleDefinitions(roles).ToListAsync(cancellationToken);
            return definitions.Select(ToResponse).ToArray();
        }

        public async Task<TenantRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken)
        {
            var roles = context.TenantRoles.Where(role => role.Id == roleId && role.DeletedAt == null);
            var definition = await ProjectRoleDefinitions(roles).SingleOrDefaultAsync(cancellationToken);
            return definition is null ? null : ToResponse(definition);
        }

        private IQueryable<RoleDefinition> ProjectRoleDefinitions(IQueryable<TenantRole> roles) =>
            roles.AsNoTracking().AsSingleQuery().Select(role => new RoleDefinition(
                role,
                EF.Property<uint>(role, "xmin"),
                context.TenantRoleClaims
                    .Where(claim => claim.RoleId == role.Id && claim.ClaimType == TenantPermissions.ClaimType)
                    .Select(claim => claim.ClaimValue).ToArray()));

        public async Task<Result> ReplaceMembershipsAsync(
            Guid staffId, IReadOnlyList<Guid> roleIds, Guid actorId, CancellationToken cancellationToken)
        {
            await context.LockUserAsync(staffId, cancellationToken);
            if (!await StaffExistsAsync(staffId, cancellationToken))
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            var current = await context.TenantUserRoles.Where(link => link.TenantUserId == staffId).ToListAsync(cancellationToken);
            var assigned = current.Select(link => link.RoleId).ToHashSet();
            foreach (var roleId in assigned.Union(roleIds).Order())
            {
                await context.LockRoleAsync(roleId, cancellationToken);
            }

            var roles = await context.TenantRoles
                .Where(role => roleIds.Contains(role.Id) && role.DeletedAt == null)
                .ToListAsync(cancellationToken);
            if (roles.Count != roleIds.Count)
            {
                return Result.Failure(TenantRoleErrors.NotFound());
            }

            if (roles.Any(role => role.IsOwnerRole))
            {
                return Result.Failure(AuthErrors.Forbidden());
            }

            foreach (var link in current.Where(link => !roleIds.Contains(link.RoleId)).OrderBy(link => link.RoleId))
            {
                context.TenantUserRoles.Remove(link);
                var audit = await AuditMembershipAsync(staffId, actorId, link.RoleId, false, cancellationToken);
                if (audit.IsFailure)
                {
                    return audit;
                }
            }

            var now = timeProvider.GetUtcNow();
            foreach (var roleId in roleIds.Where(id => !assigned.Contains(id)).Order())
            {
                context.TenantUserRoles.Add(TenantUserRole.Create(Guid.CreateVersion7(), staffId, roleId, now));
                var audit = await AuditMembershipAsync(staffId, actorId, roleId, true, cancellationToken);
                if (audit.IsFailure)
                {
                    return audit;
                }
            }

            return Result.Success();
        }

        public async Task<Result> ReplacePersonalPermissionsAsync(
            Guid staffId, IReadOnlyList<string> permissions, Guid actorId, CancellationToken cancellationToken)
        {
            await context.LockUserAsync(staffId, cancellationToken);
            if (!await StaffExistsAsync(staffId, cancellationToken))
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            var claims = await context.TenantUserClaims
                .Where(claim => claim.TenantUserId == staffId && claim.ClaimType == TenantPermissions.ClaimType)
                .ToListAsync(cancellationToken);
            var before = FormatPermissions(claims.Select(claim => claim.ClaimValue!));
            var after = FormatPermissions(permissions);
            if (before == after)
            {
                return Result.Success();
            }

            context.TenantUserClaims.RemoveRange(claims.Where(claim => !permissions.Contains(claim.ClaimValue, StringComparer.Ordinal)));
            var now = timeProvider.GetUtcNow();
            foreach (var code in permissions.Except(claims.Select(claim => claim.ClaimValue), StringComparer.Ordinal))
            {
                context.TenantUserClaims.Add(TenantUserClaim.Create(Guid.CreateVersion7(), staffId, TenantPermissions.ClaimType, code, now));
            }

            return await AuditAsync("staff.permissions.updated", "tenant_users", staffId, actorId,
                TenantAuditChanges.Update([new("PersonalPermissionCodes", before, after)]), cancellationToken);
        }

        public Task<bool> StaffExistsAsync(Guid staffId, CancellationToken cancellationToken) =>
            context.TenantUsers.AnyAsync(user => user.Id == staffId && user.UserKind == TenantUserKind.Staff, cancellationToken);

        private Task<TenantRole?> FindActiveRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
            context.TenantRoles.SingleOrDefaultAsync(role => role.Id == roleId && role.DeletedAt == null, cancellationToken);

        private Task<bool> NameIsReservedAsync(string name, Guid? exceptId, CancellationToken cancellationToken) =>
            name == TenantRole.NormalizedOwnerName
                ? Task.FromResult(true)
                : context.TenantRoles.AnyAsync(role => role.NormalizedName == name && role.Id != exceptId, cancellationToken);

        private void ReplaceRoleClaims(
            Guid roleId, IReadOnlyList<TenantRoleClaim> current, IReadOnlyList<string> permissions, DateTimeOffset now)
        {
            context.TenantRoleClaims.RemoveRange(current.Where(claim => !permissions.Contains(claim.ClaimValue, StringComparer.Ordinal)));
            foreach (var code in permissions.Except(current.Select(claim => claim.ClaimValue), StringComparer.Ordinal))
            {
                context.TenantRoleClaims.Add(TenantRoleClaim.Create(Guid.CreateVersion7(), roleId, TenantPermissions.ClaimType, code, now));
            }
        }

        private async Task<Result<TenantRoleResponse>> SaveRoleAsync(
            TenantRole role, IReadOnlyList<string> permissions, string action, TenantAuditChanges changes, Guid actorId,
            CancellationToken cancellationToken)
        {
            var audit = await AuditAsync(action, "tenant_roles", role.Id, actorId, changes, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<TenantRoleResponse>(audit.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<TenantRoleResponse>(TenantRoleErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_tenant_roles_normalized_name" })
            {
                return Result.Failure<TenantRoleResponse>(TenantRoleErrors.NameConflict());
            }

            return Result.Success(ToResponse(role, permissions, RoleVersion(role)));
        }

        private Task<Result> AuditMembershipAsync(Guid staffId, Guid actorId, Guid roleId, bool granted, CancellationToken cancellationToken)
        {
            var value = roleId.ToString("D");
            var changes = TenantAuditChanges.Update([new("RoleId", granted ? null : value, granted ? value : null)]);
            return AuditAsync(granted ? "staff.roles.assigned" : "staff.roles.removed", "tenant_users", staffId, actorId, changes, cancellationToken);
        }

        private Task<Result> AuditAsync(
            string action, string targetType, Guid targetId, Guid actorId, TenantAuditChanges changes, CancellationToken cancellationToken) =>
            auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                ActorUserId = actorId,
                Changes = changes,
            }, cancellationToken);

        private uint RoleVersion(TenantRole role) => context.Entry(role).Property<uint>("xmin").CurrentValue;

        private static TenantRoleResponse ToResponse(RoleDefinition definition) =>
            ToResponse(definition.Role, TenantPermissions.StaffUnion(definition.Codes), definition.Version);

        private static TenantRoleResponse ToResponse(TenantRole role, IReadOnlyList<string> permissions, uint version) =>
            new(role.Id, role.Name, role.IsOwnerRole ? TenantPermissions.All : permissions.Order(StringComparer.Ordinal).ToArray(),
                version.ToString(CultureInfo.InvariantCulture));

        private static string FormatPermissions(IEnumerable<string> permissions) =>
            string.Join(",", permissions.Order(StringComparer.Ordinal));

        private sealed record RoleDefinition(TenantRole Role, uint Version, string?[] Codes);
    }
}
