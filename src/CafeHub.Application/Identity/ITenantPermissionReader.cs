namespace CafeHub.Application.Identity
{
    public interface ITenantPermissionReader
    {
        Task<IReadOnlyList<string>> ReadAsync(Guid userId, CancellationToken cancellationToken, Guid? expectedBranchId = null);
    }
}
