using System.Globalization;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff;

namespace CafeHub.Application.Roles
{
    public sealed class TenantRoleAdministrationHandler(
        ITenantRoleAdministration roles,
        ITenantPermissionReader permissions,
        ICurrentTenantUser currentUser) :
        ICommandHandler<CreateTenantRole, TenantRoleResponse>,
        ICommandHandler<UpdateTenantRole, TenantRoleResponse>,
        ICommandHandler<RetireTenantRole>,
        IQueryHandler<ListTenantRoles, IReadOnlyList<TenantRoleResponse>>,
        IQueryHandler<GetTenantRole, TenantRoleResponse>,
        ICommandHandler<ReplaceStaffRoles>,
        ICommandHandler<ReplaceStaffPermissions>,
        IQueryHandler<GetStaffEffectivePermissions, IReadOnlyList<string>>
    {
        public Task<Result<TenantRoleResponse>> Handle(CreateTenantRole request, CancellationToken cancellationToken)
        {
            if (OwnerId is not { } actorId)
            {
                return Task.FromResult(Result.Failure<TenantRoleResponse>(AuthErrors.Forbidden()));
            }

            var failure = ValidateDefinition(request.Name, request.PermissionCodes);
            return failure is null
                ? roles.CreateRoleAsync(request.Name!.Trim(), request.PermissionCodes!.Distinct(StringComparer.Ordinal).ToArray(), actorId, cancellationToken)
                : Task.FromResult(Result.Failure<TenantRoleResponse>(failure));
        }

        public Task<Result<TenantRoleResponse>> Handle(UpdateTenantRole request, CancellationToken cancellationToken)
        {
            if (OwnerId is not { } actorId)
            {
                return Task.FromResult(Result.Failure<TenantRoleResponse>(AuthErrors.Forbidden()));
            }

            var failure = ValidateDefinition(request.Name, request.PermissionCodes);
            if (failure is not null)
            {
                return Task.FromResult(Result.Failure<TenantRoleResponse>(failure));
            }

            if (!uint.TryParse(request.ConcurrencyToken, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                return Task.FromResult(Result.Failure<TenantRoleResponse>(
                    Invalid("concurrencyToken", "Provide a valid role concurrency token.")));
            }

            return roles.UpdateRoleAsync(request.RoleId, request.Name!.Trim(),
                request.PermissionCodes!.Distinct(StringComparer.Ordinal).ToArray(), version, actorId, cancellationToken);
        }

        public Task<Result> Handle(RetireTenantRole request, CancellationToken cancellationToken) =>
            OwnerId is { } actorId
                ? roles.RetireRoleAsync(request.RoleId, actorId, cancellationToken)
                : Task.FromResult(Result.Failure(AuthErrors.Forbidden()));

        public async Task<Result<IReadOnlyList<TenantRoleResponse>>> Handle(ListTenantRoles request, CancellationToken cancellationToken) =>
            OwnerId.HasValue
                ? Result.Success(await roles.ListRolesAsync(cancellationToken))
                : Result.Failure<IReadOnlyList<TenantRoleResponse>>(AuthErrors.Forbidden());

        public async Task<Result<TenantRoleResponse>> Handle(GetTenantRole request, CancellationToken cancellationToken)
        {
            if (!OwnerId.HasValue)
            {
                return Result.Failure<TenantRoleResponse>(AuthErrors.Forbidden());
            }

            var role = await roles.GetRoleAsync(request.RoleId, cancellationToken);
            return role is null ? Result.Failure<TenantRoleResponse>(TenantRoleErrors.NotFound()) : Result.Success(role);
        }

        public Task<Result> Handle(ReplaceStaffRoles request, CancellationToken cancellationToken)
        {
            if (OwnerId is not { } actorId)
            {
                return Task.FromResult(Result.Failure(AuthErrors.Forbidden()));
            }

            if (request.RoleIds is null || request.RoleIds.Contains(Guid.Empty))
            {
                return Task.FromResult(Result.Failure(Invalid("roleIds", "Provide an array of valid role IDs.")));
            }

            return roles.ReplaceMembershipsAsync(request.StaffId, request.RoleIds.Distinct().ToArray(), actorId, cancellationToken);
        }

        public Task<Result> Handle(ReplaceStaffPermissions request, CancellationToken cancellationToken)
        {
            if (OwnerId is not { } actorId)
            {
                return Task.FromResult(Result.Failure(AuthErrors.Forbidden()));
            }

            var failure = ValidatePermissions(request.PermissionCodes);
            return failure is null
                ? roles.ReplacePersonalPermissionsAsync(request.StaffId,
                    request.PermissionCodes!.Distinct(StringComparer.Ordinal).ToArray(), actorId, cancellationToken)
                : Task.FromResult(Result.Failure(failure));
        }

        public async Task<Result<IReadOnlyList<string>>> Handle(GetStaffEffectivePermissions request, CancellationToken cancellationToken)
        {
            if (!OwnerId.HasValue)
            {
                return Result.Failure<IReadOnlyList<string>>(AuthErrors.Forbidden());
            }

            return await roles.StaffExistsAsync(request.StaffId, cancellationToken)
                ? Result.Success(await permissions.ReadAsync(request.StaffId, cancellationToken))
                : Result.Failure<IReadOnlyList<string>>(StaffErrors.NotFound());
        }

        private Guid? OwnerId => currentUser.AccountAccess is { IsStaff: false } ? currentUser.UserId : null;

        private static Error? ValidateDefinition(string? name, IReadOnlyList<string>? codes)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256)
            {
                return Invalid("name", "Provide a role name of 1 to 256 characters.");
            }

            if (name.Contains('\0'))
            {
                return Invalid("name", "The role name must not contain a null character.");
            }

            return ValidatePermissions(codes);
        }

        private static Error? ValidatePermissions(IReadOnlyList<string>? codes) =>
            codes is null || codes.Any(code => !TenantPermissions.IsStaffGrant(code))
                ? Invalid("permissionCodes", "Provide an array of grantable permission codes.")
                : null;

        private static ValidationError Invalid(string field, string message) => ValidationError.Create([new FieldError(field, message)]);
    }
}
