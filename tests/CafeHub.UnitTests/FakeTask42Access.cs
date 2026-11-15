using CafeHub.Application.Identity;

namespace CafeHub.UnitTests
{
    internal sealed class FakeTask42Access : ICurrentTenantUser
    {
        public bool IsAuthenticated => AccountAccess is not null;
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; } = Guid.NewGuid();
        public IReadOnlyCollection<string> Roles => [];
        public Guid? BranchId => AccountAccess?.BranchId;
        public TenantAccountAccess? AccountAccess { get; set; } = new(false, null, false);
    }
}
