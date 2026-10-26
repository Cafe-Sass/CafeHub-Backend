using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenancy.Application
{
    public sealed class TenantStateGuardTests
    {
        [Fact]
        public void IsUsable_OnlyAllowsActiveForEveryDeclaredAndUnknownState()
        {
            foreach (var state in Enum.GetValues<TenantLifecycleStatus>())
            {
                Assert.Equal(state == TenantLifecycleStatus.Active, TenantStateAvailability.IsUsable(state));
            }

            Assert.False(TenantStateAvailability.IsUsable((TenantLifecycleStatus)int.MaxValue));
        }

        [Theory]
        [InlineData(TenantLifecycleStatus.Provisioning)]
        [InlineData(TenantLifecycleStatus.Suspended)]
        [InlineData(TenantLifecycleStatus.Terminated)]
        public void EnsureUsable_RejectsNonActiveStates(TenantLifecycleStatus state)
        {
            var result = new TenantStateGuard().EnsureUsable(new ResolvedTenant(Guid.NewGuid(), "cafe", state, "ref"));

            Assert.True(result.IsFailure);
            Assert.Equal(TenancyErrors.TenantUnavailable(), result.Error);
        }
    }
}
