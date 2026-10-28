using CafeHub.Domain.Tenants;
using CafeHub.Domain.Tenants.Credentials;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantTests
    {
        private static readonly Guid TenantId = Guid.CreateVersion7();
        private static readonly Guid RequestId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var tenant = Tenant.Create(TenantId, RequestId, "my-cafe", TenantLifecycleStatus.Provisioning, createdAt);

            Assert.Equal(TenantId, tenant.Id);
            Assert.Equal(RequestId, tenant.OnboardingRequestId);
            Assert.Equal("my-cafe", tenant.Slug);
            Assert.Equal(TenantLifecycleStatus.Provisioning, tenant.LifecycleStatus);
            Assert.Equal(createdAt, tenant.CreatedAt);
            Assert.Equal(createdAt, tenant.UpdatedAt);
            Assert.Null(tenant.CreatedBy);
            Assert.Null(tenant.UpdatedBy);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("a1b2c3")]
        [InlineData("my-cafe")]
        [InlineData("cafe-1")]
        public void Create_AcceptsValidSlug(string slug)
        {
            var tenant = Tenant.Create(TenantId, RequestId, slug, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal(slug, tenant.Slug);
        }

        [Fact]
        public void Create_AcceptsMinimumLengthSlug()
        {
            var tenant = Tenant.Create(TenantId, RequestId, "abc", TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal("abc", tenant.Slug);
        }

        [Fact]
        public void Create_AcceptsMaximumLengthSlug()
        {
            var slug = "a" + new string('b', 38) + "z";

            var tenant = Tenant.Create(TenantId, RequestId, slug, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal(40, tenant.Slug.Length);
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("a")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Create_RejectsShortOrEmptySlug(string? slug)
        {
            Assert.Throws<ArgumentException>(() =>
                Tenant.Create(TenantId, RequestId, slug!, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsTooLongSlug()
        {
            var slug = "a" + new string('b', 39) + "z";

            Assert.Throws<ArgumentException>(() =>
                Tenant.Create(TenantId, RequestId, slug, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData("Abc")]
        [InlineData("ABC")]
        public void Create_RejectsUppercaseSlug(string slug)
        {
            Assert.Throws<ArgumentException>(() =>
                Tenant.Create(TenantId, RequestId, slug, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData("ab_cd")]
        [InlineData("ab cd")]
        [InlineData("-abc")]
        [InlineData("abc-")]
        public void Create_RejectsInvalidSlugFormat(string slug)
        {
            Assert.Throws<ArgumentException>(() =>
                Tenant.Create(TenantId, RequestId, slug, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("ADMIN")]
        [InlineData("api")]
        [InlineData("www")]
        [InlineData("t")]
        [InlineData("login")]
        [InlineData("signup")]
        [InlineData("static")]
        [InlineData("assets")]
        [InlineData("health")]
        [InlineData("support")]
        [InlineData("platform")]
        public void Create_RejectsReservedSlug(string slug)
        {
            Assert.Throws<ArgumentException>(() =>
                Tenant.Create(TenantId, RequestId, slug, TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyOnboardingRequestId()
        {
            Assert.Throws<ArgumentException>(() =>
                Tenant.Create(Guid.Empty, Guid.Empty, "my-cafe", TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void TransitionTo_ChangesLifecycleStatus_AndUpdatesTimestamp()
        {
            var tenant = Tenant.Create(TenantId, RequestId, "my-cafe", TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

            tenant.TransitionTo(TenantLifecycleStatus.Active, now);

            Assert.Equal(TenantLifecycleStatus.Active, tenant.LifecycleStatus);
            Assert.Equal(now, tenant.UpdatedAt);
        }

        [Fact]
        public void TransitionTo_RejectsUndefinedStatus()
        {
            var tenant = Tenant.Create(TenantId, RequestId, "my-cafe", TenantLifecycleStatus.Provisioning, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() =>
                tenant.TransitionTo((TenantLifecycleStatus)99, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }

    public sealed class TenantDatabaseTests
    {
        private static readonly Guid DatabaseId = Guid.CreateVersion7();
        private static readonly Guid TenantId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var database = TenantDatabase.Create(DatabaseId, TenantId, "cafe-db-ref", TenantDatabaseStatus.Provisioned, createdAt);

            Assert.Equal(DatabaseId, database.Id);
            Assert.Equal(TenantId, database.TenantId);
            Assert.Equal("cafe-db-ref", database.SecretReference);
            Assert.Equal(TenantDatabaseStatus.Provisioned, database.Status);
            Assert.Equal(createdAt, database.CreatedAt);
            Assert.Equal(createdAt, database.UpdatedAt);
            Assert.Null(database.CreatedBy);
            Assert.Null(database.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyIds()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantDatabase.Create(Guid.Empty, Guid.Empty, "cafe-db-ref", TenantDatabaseStatus.Provisioned, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_RejectsInvalidSecretReference(string? secretReference)
        {
            Assert.Throws<ArgumentException>(() =>
                TenantDatabase.Create(DatabaseId, TenantId, secretReference!, TenantDatabaseStatus.Provisioned, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Theory]
        [InlineData("-abc")]
        [InlineData("ABC")]
        [InlineData("abc_")]
        public void Create_RejectsInvalidSecretReferenceFormat(string secretReference)
        {
            Assert.Throws<ArgumentException>(() =>
                TenantDatabase.Create(DatabaseId, TenantId, secretReference, TenantDatabaseStatus.Provisioned, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsOverlyLongSecretReference()
        {
            var longRef = new string('a', 129);

            Assert.Throws<ArgumentException>(() =>
                TenantDatabase.Create(DatabaseId, TenantId, longRef, TenantDatabaseStatus.Provisioned, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_AcceptsMinimumLengthSecretReference()
        {
            var database = TenantDatabase.Create(DatabaseId, TenantId, "a", TenantDatabaseStatus.Provisioned, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal("a", database.SecretReference);
        }

        [Fact]
        public void Create_AcceptsDemoCarlosTenantScopedReference()
        {
            var tenantId = Guid.Parse("01a1807f-8ce3-7fc3-a78f-04d0c485641d");
            var secretReference = "v1-01a1807f8ce37fc3a78f04d0c485641d";

            var database = TenantDatabase.Create(
                DatabaseId,
                tenantId,
                secretReference,
                TenantDatabaseStatus.Provisioned,
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal(TenantDatabaseCredentialReference.CreateV1(tenantId), database.SecretReference);
        }

        [Fact]
        public void Create_RejectsTenantScopedReferenceOwnedByAnotherTenant()
        {
            var otherTenantId = Guid.CreateVersion7();

            Assert.Throws<ArgumentException>(() =>
                TenantDatabase.Create(
                    DatabaseId,
                    TenantId,
                    TenantDatabaseCredentialReference.CreateV1(otherTenantId),
                    TenantDatabaseStatus.Provisioned,
                    new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }
}
