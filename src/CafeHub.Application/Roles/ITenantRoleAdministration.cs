using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Roles
{
    public interface ITenantRoleAdministration
    {
        Task<Result<TenantRoleResponse>> CreateRoleAsync(string name, IReadOnlyList<string> permissions, Guid actorId, CancellationToken cancellationToken);
        Task<Result<TenantRoleResponse>> UpdateRoleAsync(Guid roleId, string name, IReadOnlyList<string> permissions, uint version, Guid actorId, CancellationToken cancellationToken);
        Task<Result> RetireRoleAsync(Guid roleId, Guid actorId, CancellationToken cancellationToken);
        Task<IReadOnlyList<TenantRoleResponse>> ListRolesAsync(CancellationToken cancellationToken);
        Task<TenantRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken);
        Task<Result> ReplaceMembershipsAsync(Guid staffId, IReadOnlyList<Guid> roleIds, Guid actorId, CancellationToken cancellationToken);
        Task<Result> ReplacePersonalPermissionsAsync(Guid staffId, IReadOnlyList<string> permissions, Guid actorId, CancellationToken cancellationToken);
        Task<bool> StaffExistsAsync(Guid staffId, CancellationToken cancellationToken);
    }
}
