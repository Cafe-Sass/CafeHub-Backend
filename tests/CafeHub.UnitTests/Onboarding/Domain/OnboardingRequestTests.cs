using CafeHub.Domain.Onboarding;

namespace CafeHub.UnitTests.Onboarding.Domain
{
    public sealed class OnboardingRequestTests
    {
        private static readonly Guid RequestId = Guid.CreateVersion7();
        private static readonly Guid PlanVersionId = Guid.CreateVersion7();

        [Fact]
        public void CreateForPublicRegistration_SetsInitialState_AwaitingPayment()
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                PlanVersionId,
                "Owner.Name@Example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "AQAAAAIAAY...".PadLeft(512, 'A'),
                "my-cafe",
                TestData.CreatedAt);

            Assert.Equal(RequestId, request.Id);
            Assert.Equal(OnboardingRequestCreatedVia.PublicRegistration, request.CreatedVia);
            Assert.Null(request.CreatedBy);
            Assert.Equal(PlanVersionId, request.PlanVersionId);
            Assert.Equal("owner.name@example.com", request.OwnerEmail);
            Assert.Equal("Owner Name", request.OwnerFullName);
            Assert.Null(request.OwnerPhoneNumber);
            Assert.Equal("Cafe Name", request.CafeName);
            Assert.False(string.IsNullOrEmpty(request.OwnerPasswordHash));
            Assert.Equal("my-cafe", request.TenantSlug);
            Assert.Equal(OnboardingRequestStatus.AwaitingPayment, request.Status);
            Assert.Equal(TestData.CreatedAt, request.CreatedAt);
            Assert.Equal(TestData.CreatedAt, request.UpdatedAt);
            Assert.Null(request.UpdatedBy);
        }

        [Fact]
        public void CreateForPublicRegistration_NormalizesEmailToLowercase()
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                PlanVersionId,
                "OWNER.NAME@EXAMPLE.COM",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                "my-cafe",
                TestData.CreatedAt);

            Assert.Equal("owner.name@example.com", request.OwnerEmail);
        }

        [Theory]
        [InlineData("", nameof(OnboardingRequest.OwnerFullName))]
        [InlineData("   ", nameof(OnboardingRequest.OwnerFullName))]
        [InlineData("a", nameof(OnboardingRequest.TenantSlug))]
        [InlineData("A", nameof(OnboardingRequest.TenantSlug))]
        [InlineData("ab_cd", nameof(OnboardingRequest.TenantSlug))]
        [InlineData("admin", nameof(OnboardingRequest.TenantSlug))]
        [InlineData("API", nameof(OnboardingRequest.TenantSlug))]
        public void CreateForPublicRegistration_RejectsInvalidFields(string? value, string paramName)
        {
            Assert.Throws<ArgumentException>(() =>
                CreateRequestWithFieldOverridden(
                    value,
                    paramName,
                    "owner@example.com",
                    "Owner Name",
                    null,
                    "Cafe Name",
                    "hash",
                    "my-cafe",
                    TestData.CreatedAt));
        }

        private static OnboardingRequest CreateRequestWithFieldOverridden(
            string? value,
            string paramName,
            string ownerEmail,
            string ownerFullName,
            string? ownerPhoneNumber,
            string cafeName,
            string ownerPasswordHash,
            string tenantSlug,
            DateTimeOffset createdAt)
        {
            return paramName switch
            {
                nameof(OnboardingRequest.OwnerFullName) => OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, ownerEmail, value!, ownerPhoneNumber, cafeName, ownerPasswordHash, tenantSlug, createdAt),
                nameof(OnboardingRequest.TenantSlug) => OnboardingRequest.CreateForPublicRegistration(
                    RequestId, PlanVersionId, ownerEmail, ownerFullName, ownerPhoneNumber, cafeName, ownerPasswordHash, value ?? "my-cafe", createdAt),
                _ => throw new InvalidOperationException($"Unknown parameter {paramName}.")
            };
        }

        [Fact]
        public void CreateForPublicRegistration_RejectsEmptyPasswordHash()
        {
            Assert.Throws<ArgumentException>(() =>
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId,
                    PlanVersionId,
                    "owner@example.com",
                    "Owner Name",
                    null,
                    "Cafe Name",
                    string.Empty,
                    "my-cafe",
                    TestData.CreatedAt));
        }

        [Fact]
        public void CreateForPublicRegistration_RejectsPasswordHashLongerThan512()
        {
            var longHash = new string('x', 513);
            Assert.Throws<ArgumentException>(() =>
                OnboardingRequest.CreateForPublicRegistration(
                    RequestId,
                    PlanVersionId,
                    "owner@example.com",
                    "Owner Name",
                    null,
                    "Cafe Name",
                    longHash,
                    "my-cafe",
                    TestData.CreatedAt));
        }

        [Fact]
        public void CreateForPublicRegistration_AcceptsMinimumAndMaximumSlugLength()
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                PlanVersionId,
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                "abc",
                TestData.CreatedAt);

            Assert.Equal("abc", request.TenantSlug);

            var longSlug = new string('a', 38);
            var longRequest = OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                PlanVersionId,
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                $"a{longSlug}z",
                TestData.CreatedAt);

            Assert.Equal(40, longRequest.TenantSlug.Length);
        }

        [Fact]
        public void CreateForPublicRegistration_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                OnboardingRequest.CreateForPublicRegistration(
                    Guid.Empty,
                    PlanVersionId,
                    "owner@example.com",
                    "Owner Name",
                    null,
                    "Cafe Name",
                    "hash",
                    "my-cafe",
                    TestData.CreatedAt));
        }

        [Fact]
        public void CreateForSupport_KeepsEmailAsTyped_AndSetsCreatedBy()
        {
            var request = OnboardingRequest.CreateForSupport(
                RequestId,
                PlanVersionId,
                "Owner.Name@Example.com",
                "Owner Name",
                null,
                "Cafe Name",
                null,
                "my-cafe",
                TestData.CreatedAt,
                Guid.CreateVersion7());

            Assert.Equal("Owner.Name@Example.com", request.OwnerEmail);
            Assert.Equal(OnboardingRequestCreatedVia.Support, request.CreatedVia);
            Assert.NotNull(request.CreatedBy);
            Assert.Equal(OnboardingRequestStatus.AwaitingPayment, request.Status);
        }
    }

    internal static class TestData
    {
        public static readonly DateTimeOffset CreatedAt = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
    }
}
