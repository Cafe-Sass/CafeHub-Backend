using CafeHub.Application.Identity;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class RefundRequestPermissionsTests
    {
        [Fact]
        public void RefundPermissionsAreCataloguedAndGrantableToStaff()
        {
            Assert.Equal("refunds.view", TenantPermissions.RefundsView);
            Assert.Equal("refunds.manage", TenantPermissions.RefundsManage);
            Assert.Contains(TenantPermissions.RefundsView, TenantPermissions.All);
            Assert.Contains(TenantPermissions.RefundsManage, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.RefundsView));
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.RefundsManage));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.RefundsView));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.RefundsManage));
        }
    }
}
