namespace CafeHub.Application.Identity
{
    public interface ICurrentTenantUser
    {
        bool IsAuthenticated { get; }

        Guid? UserId { get; }

        Guid? TenantId { get; }

        IReadOnlyCollection<string> Roles { get; }
    }
}
