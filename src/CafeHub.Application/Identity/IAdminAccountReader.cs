namespace CafeHub.Application.Identity
{
    public interface IAdminAccountReader
    {
        Task<AuthenticatedAdmin?> FindActiveByIdAsync(Guid adminId, CancellationToken cancellationToken);
    }
}
