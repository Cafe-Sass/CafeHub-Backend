namespace CafeHub.Application.Identity
{
    public interface ITenantAccountReader
    {
        Task<TenantAccountAccess?> FindActiveAsync(Guid userId, CancellationToken cancellationToken);
    }
}
