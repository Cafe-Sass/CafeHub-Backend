using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests
{
    public sealed class TenantContextTests
    {
        [Fact]
        public void Create_Throws_WhenTenantIdIsEmpty()
        {
            var tenantId = Guid.Empty;

            var exception = Record.Exception(() => TenantContext.Create(tenantId, "cafe-alpha", true));

            Assert.IsType<InvalidTenantContextException>(exception);
        }

        [Fact]
        public void Create_Throws_WhenSecretReferenceIsBlank()
        {
            var tenantId = Guid.NewGuid();

            var exception = Record.Exception(() => TenantContext.Create(tenantId, "\t   \n", true));

            Assert.IsType<InvalidTenantContextException>(exception);
        }

        [Fact]
        public void Create_AcceptsValidValues()
        {
            var tenantId = Guid.NewGuid();

            var tenantContext = TenantContext.Create(tenantId, "cafe-alpha", true);

            Assert.Equal(tenantId, tenantContext.TenantId);
            Assert.Equal("cafe-alpha", tenantContext.SecretReference);
            Assert.True(tenantContext.IsActive);
        }
    }
}
