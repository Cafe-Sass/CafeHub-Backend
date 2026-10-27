using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.UnitTests
{
    public sealed class TenantAuditWriterTests
    {
        private static readonly DateTimeOffset RecordedAt = DateTimeOffset.Parse("2026-10-27T12:30:00Z");
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        private static (TenantAuditWriter Writer, TenantDbContext Context) CreateWriter(
            bool isAuthenticated = false,
            Guid? currentUserId = null)
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=unit-test")
                .Options;
            var context = new TenantDbContext(options);
            var writer = new TenantAuditWriter(
                new StubCurrentTenantUser(isAuthenticated, currentUserId),
                new FixedTimeProvider(RecordedAt),
                context,
                new StubTenantContextAccessor());
            return (writer, context);
        }

        [Fact]
        public async Task WriteAsync_CapturesAuthenticatedUserAndTenantRecordFields()
        {
            var userId = Guid.NewGuid();
            var (writer, context) = CreateWriter(true, userId);
            using (context)
            {
                var result = await writer.WriteAsync(new TenantAuditEntry
                {
                    Action = "tenant.login.succeeded",
                    TargetType = "tenant_user",
                    TargetId = userId,
                    Reason = TenantAuditReason.InvalidCredentials,
                }, CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.NotEqual(Guid.Empty, record.Id);
                Assert.Equal(RecordedAt, record.RecordedAt);
                Assert.Equal(userId, record.ActorUserId);
                Assert.Equal(TenantAuditRecordKind.User, record.ActorKind);
                Assert.Equal("tenant.login.succeeded", record.ActionCode);
                Assert.Equal("tenant_user", record.SubjectType);
                Assert.Equal(userId, record.SubjectId);
                Assert.Null(record.BeforeState);
                Assert.Null(record.AfterState);
            }
        }

        [Fact]
        public async Task WriteAsync_UsesKnownActorOrSystemWhenNoAuthenticatedUserExists()
        {
            var knownUserId = Guid.NewGuid();
            var (knownActorWriter, knownActorContext) = CreateWriter();
            using (knownActorContext)
            {
                var result = await knownActorWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "tenant.login.failed",
                    TargetType = "tenant_user",
                    TargetId = knownUserId,
                    ActorUserId = knownUserId,
                }, CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(knownActorContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Equal(TenantAuditRecordKind.User, record.ActorKind);
                Assert.Equal(knownUserId, record.ActorUserId);
            }

            var (unknownActorWriter, unknownActorContext) = CreateWriter();
            using (unknownActorContext)
            {
                var result = await unknownActorWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "tenant.login.failed",
                    TargetType = "tenant_user",
                }, CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(unknownActorContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Equal(TenantAuditRecordKind.System, record.ActorKind);
                Assert.Null(record.ActorUserId);
            }
        }

        [Fact]
        public void TenantAuditEntry_ContainsOnlyAuditMetadataAndNoSecretBearingMembers()
        {
            var properties = typeof(TenantAuditEntry).GetProperties().Select(property => property.Name).Order().ToArray();

            Assert.Equal(new[] { "Action", "ActorUserId", "Reason", "TargetId", "TargetType" }.Order(), properties);
            Assert.DoesNotContain(properties, property => property.Contains("email", StringComparison.OrdinalIgnoreCase)
                || property.Contains("password", StringComparison.OrdinalIgnoreCase)
                || property.Contains("hash", StringComparison.OrdinalIgnoreCase)
                || property.Contains("token", StringComparison.OrdinalIgnoreCase)
                || property.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task WriteAsync_RejectsActionCodesOutsideTheTableContract()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(new TenantAuditEntry
                {
                    Action = "Tenant login",
                    TargetType = "tenant_user",
                }, CancellationToken.None);

                Assert.True(result.IsFailure);
                Assert.Empty(context.ChangeTracker.Entries<TenantAuditRecord>());
            }
        }

        private sealed class StubCurrentTenantUser(bool isAuthenticated, Guid? userId) : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; } = isAuthenticated;

            public Guid? UserId { get; } = userId;

            public Guid? TenantId => IsAuthenticated ? TenantIdValue : null;

            public IReadOnlyCollection<string> Roles => Array.Empty<string>();

            private static Guid TenantIdValue => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        }

        private sealed class StubTenantContextAccessor : ITenantContextAccessor
        {
            public TenantContext GetRequired() => TenantContext.Create(TenantId, "tenant-secret-reference", true);
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
