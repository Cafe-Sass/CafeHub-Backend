using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantAuditRecordTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid ActorId = Guid.CreateVersion7();
        private static readonly Guid SubjectId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var record = TenantAuditRecord.Create(
                Id,
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ActorId,
                TenantAuditRecordKind.User,
                "tenants.updated",
                "tenant_user",
                SubjectId,
                "{\"name\":\"old\"}",
                "{\"name\":\"new\"}");

            Assert.Equal(Id, record.Id);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), record.RecordedAt);
            Assert.Equal(ActorId, record.ActorUserId);
            Assert.Equal(TenantAuditRecordKind.User, record.ActorKind);
            Assert.Equal("tenants.updated", record.ActionCode);
            Assert.Equal("tenant_user", record.SubjectType);
            Assert.Equal(SubjectId, record.SubjectId);
            Assert.Equal("{\"name\":\"old\"}", record.BeforeState);
            Assert.Equal("{\"name\":\"new\"}", record.AfterState);
        }

        [Fact]
        public void Create_AllowsNullActorUserId_SystemWrite()
        {
            var record = TenantAuditRecord.Create(
                Id,
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                null,
                TenantAuditRecordKind.System,
                "provisioning.seeded",
                "tenant_database",
                null,
                null,
                null);

            Assert.Null(record.ActorUserId);
            Assert.Equal(TenantAuditRecordKind.System, record.ActorKind);
            Assert.Null(record.SubjectId);
            Assert.Null(record.BeforeState);
            Assert.Null(record.AfterState);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantAuditRecord.Create(
                    Guid.Empty, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                    null, TenantAuditRecordKind.System, "action", "subject", null, null, null));
        }

        [Fact]
        public void Create_RejectsUndefinedActorKind()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantAuditRecord.Create(
                    Id, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                    null, (TenantAuditRecordKind)99, "action", "subject", null, null, null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_RejectsEmptyActionCode(string? actionCode)
        {
            Assert.Throws<ArgumentException>(() =>
                TenantAuditRecord.Create(
                    Id, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                    null, TenantAuditRecordKind.System, actionCode!, "subject", null, null, null));
        }

        [Fact]
        public void Create_RejectsOverlyLongActionCode()
        {
            var longCode = new string('a', 129);

            Assert.Throws<ArgumentException>(() =>
                TenantAuditRecord.Create(
                    Id, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                    null, TenantAuditRecordKind.System, longCode, "subject", null, null, null));
        }

        [Fact]
        public void Create_RejectsEmptySubjectType()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantAuditRecord.Create(
                    Id, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                    null, TenantAuditRecordKind.System, "action", "", null, null, null));
        }
    }
}
