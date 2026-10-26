using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenancy.Application
{
    public sealed class TenantResolverTests
    {
        private sealed class FakeTenantRegistryReader : ITenantRegistryReader
        {
            public Dictionary<string, TenantRegistration> BySlug { get; } = new(StringComparer.Ordinal);

            public TenantRegistration? ByIdResult { get; set; }

            public int CallCount { get; private set; }

            public Task<TenantRegistration?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken)
            {
                CallCount++;
                BySlug.TryGetValue(normalizedSlug, out var registration);
                return Task.FromResult(registration);
            }

            public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult(ByIdResult);
            }
        }

        private sealed class FakeTenantContextSetter : ITenantContextSetter
        {
            public int CallCount { get; private set; }

            public void Set(TenantContext tenantContext)
            {
                CallCount++;
            }
        }

        private static readonly TenantRegistration KnownRegistration =
            new(Guid.CreateVersion7(), "my-cafe", TenantLifecycleStatus.Provisioning, "v1");

        private static Result<ResolvedTenant> Resolve(FakeTenantRegistryReader reader, string? rawSlug)
        {
            var resolver = new TenantResolver(reader);
            return resolver.ResolveAsync(rawSlug!, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData("my-cafe")]
        [InlineData("My-Cafe")]
        [InlineData("MY-CAFE")]
        public async Task KnownSlug_ResolvesCaseInsensitively_AndMapsEveryField(string rawSlug)
        {
            var reader = new FakeTenantRegistryReader();
            reader.BySlug[KnownRegistration.Slug] = KnownRegistration;

            var result = await new TenantResolver(reader).ResolveAsync(rawSlug, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var resolved = result.Value!;
            Assert.Equal(KnownRegistration.TenantId, resolved.TenantId);
            Assert.Equal(KnownRegistration.Slug, resolved.Slug);
            Assert.Equal(KnownRegistration.State, resolved.State);
            Assert.Equal(KnownRegistration.SecretReference, resolved.SecretReference);
        }

        [Fact]
        public async Task UnknownSlug_ReturnsTheUniformUnavailableResult()
        {
            var reader = new FakeTenantRegistryReader();

            var result = await new TenantResolver(reader).ResolveAsync("unknown-cafe", CancellationToken.None);

            AssertUniformUnavailable(result);
        }

        [Theory]
        [InlineData("UPPER_case")]
        [InlineData("with.dot")]
        [InlineData("with--double-hyphen")]
        [InlineData("-leading-hyphen")]
        [InlineData("trailing-hyphen-")]
        [InlineData("api")]
        [InlineData("ab")]
        public async Task MalformedSlug_ReturnsTheUniformUnavailableResult_WithoutQuerying(string rawSlug)
        {
            var reader = new FakeTenantRegistryReader();
            reader.BySlug[rawSlug] = KnownRegistration;

            var result = Resolve(reader, rawSlug);

            AssertUniformUnavailable(result);
            Assert.Equal(0, reader.CallCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task MissingSlug_ReturnsTheUniformUnavailableResult_WithoutQuerying(string? rawSlug)
        {
            var reader = new FakeTenantRegistryReader();

            var result = Resolve(reader, rawSlug);

            AssertUniformUnavailable(result);
            Assert.Equal(0, reader.CallCount);
        }

        [Fact]
        public async Task Resolver_NeverTouchesTheTenantContextSetter()
        {
            var reader = new FakeTenantRegistryReader();
            reader.BySlug[KnownRegistration.Slug] = KnownRegistration;
            var setter = new FakeTenantContextSetter();
            var resolver = new TenantResolver(reader);

            await resolver.ResolveAsync(KnownRegistration.Slug, CancellationToken.None);
            await resolver.ResolveAsync("unknown-cafe", CancellationToken.None);
            resolver.ResolveAsync("with.dot", CancellationToken.None);

            Assert.Equal(0, setter.CallCount);
        }

        [Fact]
        public void RegistrationAndResolvedRecords_CarryOnlyTheRegistryMembers()
        {
            var expectedMembers = new[] { "TenantId", "Slug", "State", "SecretReference" }
                .OrderBy(name => name, StringComparer.Ordinal);

            Assert.Equal(expectedMembers, typeof(TenantRegistration).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(expectedMembers, typeof(ResolvedTenant).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        }

        private static void AssertUniformUnavailable(Result<ResolvedTenant> result)
        {
            Assert.True(result.IsFailure);
            var error = result.Error!;
            Assert.Equal(TenancyErrors.TenantUnavailableCode, error.Code);
            Assert.Equal(TenancyErrors.TenantUnavailableMessage, error.Message);
            Assert.Equal(ErrorType.NotFound, error.Type);
        }
    }
}
