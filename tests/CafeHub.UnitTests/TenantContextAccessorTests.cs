using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Tenancy;

namespace CafeHub.UnitTests
{
    public sealed class TenantContextAccessorTests
    {
        [Fact]
        public void GetRequired_Throws_WhenNoTenantWasSet()
        {
            var accessor = new TenantContextAccessor();

            var exception = Record.Exception(() => ((ITenantContextAccessor)accessor).GetRequired());

            Assert.IsType<TenantContextMissingException>(exception);
        }

        [Fact]
        public void GetRequired_ReturnsSameInstance_AfterSet()
        {
            var accessor = new TenantContextAccessor();
            var tenantContext = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true);

            ((ITenantContextSetter)accessor).Set(tenantContext);

            Assert.Same(tenantContext, ((ITenantContextAccessor)accessor).GetRequired());
        }
    }
}
