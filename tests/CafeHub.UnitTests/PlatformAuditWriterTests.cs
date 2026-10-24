using System.Reflection;
using CafeHub.Application.Audit;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Master.Audit;

namespace CafeHub.UnitTests
{
    public sealed class PlatformAuditWriterTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        private static (PlatformAuditWriter Writer, FakeAuditStore Store, FakeAuditUnitOfWork UnitOfWork) CreateWriter(FakeCurrentUser? currentUser = null)
        {
            var store = new FakeAuditStore();
            var unitOfWork = new FakeAuditUnitOfWork();
            unitOfWork.OnCommit = store.CommitPending;
            var writer = new PlatformAuditWriter(
                currentUser ?? new FakeCurrentUser { IsAuthenticated = true, AdminId = Guid.NewGuid() },
                new MutableTimeProvider { UtcNow = FixedNow },
                store);

            return (writer, store, unitOfWork);
        }

        private static PlatformAuditEntry CreateEntry(PlatformAuditActorKind actorKind = PlatformAuditActorKind.Admin)
        {
            return new PlatformAuditEntry
            {
                ActorKind = actorKind,
                Action = "admin.login.succeeded",
                TargetType = "platform_admin",
                TargetId = Guid.NewGuid(),
                Outcome = PlatformAuditOutcome.Succeeded,
            };
        }

        [Fact]
        public void Write_AdminEntry_TakesTheActorIdFromTheCurrentUser()
        {
            var adminId = Guid.NewGuid();
            var (writer, store, _) = CreateWriter(new FakeCurrentUser { IsAuthenticated = true, AdminId = adminId });

            writer.Write(CreateEntry());

            var record = Assert.Single(store.Records);
            Assert.Equal(adminId, record.ActorPlatformAdminId);
            Assert.Equal("PlatformAdmin", record.ActorKind);
            Assert.Equal("admin.login.succeeded", record.ActionCode);
            Assert.Equal("platform_admin", record.SubjectType);
            Assert.NotNull(record.SubjectId);
        }

        [Fact]
        public void Write_SystemEntry_HasNoActorId()
        {
            var (writer, store, _) = CreateWriter();

            writer.Write(CreateEntry(PlatformAuditActorKind.System));

            var record = Assert.Single(store.Records);
            Assert.Null(record.ActorPlatformAdminId);
            Assert.Equal("System", record.ActorKind);
        }

        [Fact]
        public void Write_AdminEntry_WithAnAnonymousUser_IsRejected_WithoutUserData()
        {
            var (writer, store, _) = CreateWriter(new FakeCurrentUser { IsAuthenticated = false, AdminId = null });

            var exception = Record.Exception(() => writer.Write(CreateEntry()));

            Assert.IsType<InvalidOperationException>(exception);
            Assert.DoesNotContain("@", exception!.Message, StringComparison.Ordinal);
            Assert.Empty(store.Records);
        }

        [Fact]
        public void Write_TheTimestampComesFromTheTimeProvider()
        {
            var (writer, store, _) = CreateWriter();

            writer.Write(CreateEntry());

            Assert.Equal(FixedNow, Assert.Single(store.Records).RecordedAt);
        }

        [Fact]
        public void Write_NeverSetsTheSnapshotColumns_AndDoesNotSaveByItself()
        {
            var (writer, store, unitOfWork) = CreateWriter();

            writer.Write(CreateEntry());

            var record = Assert.Single(store.Records);
            Assert.Null(record.BeforeState);
            Assert.Null(record.AfterState);
            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(0, unitOfWork.RollbackCount);
            Assert.Empty(store.PersistedRecords);
        }

        [Fact]
        public async Task Write_WhenTheUnitOfWorkRollsBack_NothingIsPersisted()
        {
            var (writer, store, unitOfWork) = CreateWriter();

            writer.Write(CreateEntry());
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);

            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
            Assert.Empty(store.PersistedRecords);
            Assert.Single(store.Records);
        }

        [Fact]
        public async Task Write_RecordsBecomePersisted_OnlyWhenTheUnitOfWorkCommits()
        {
            var (writer, store, unitOfWork) = CreateWriter();

            writer.Write(CreateEntry());
            await unitOfWork.CommitTransactionAsync(CancellationToken.None);

            Assert.Single(store.PersistedRecords);
        }

        [Fact]
        public void TheEntryContract_ExposesNoFreeFormPayloadMember()
        {
            var propertyNames = typeof(PlatformAuditEntry)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.Equal(
                new[] { "Action", "ActorKind", "Outcome", "Reason", "TargetId", "TargetType" },
                propertyNames);
        }

        private static PlatformAuditEntry WithAction(PlatformAuditEntry entry, string action)
        {
            return new PlatformAuditEntry
            {
                ActorKind = entry.ActorKind,
                Action = action,
                TargetType = entry.TargetType,
                TargetId = entry.TargetId,
                Outcome = entry.Outcome,
                Reason = entry.Reason,
            };
        }

        private static PlatformAuditEntry WithReason(PlatformAuditEntry entry, string reason)
        {
            return new PlatformAuditEntry
            {
                ActorKind = entry.ActorKind,
                Action = entry.Action,
                TargetType = entry.TargetType,
                TargetId = entry.TargetId,
                Outcome = entry.Outcome,
                Reason = reason,
            };
        }

        private static PlatformAuditEntry WithTargetType(PlatformAuditEntry entry, string targetType)
        {
            return new PlatformAuditEntry
            {
                ActorKind = entry.ActorKind,
                Action = entry.Action,
                TargetType = targetType,
                TargetId = entry.TargetId,
                Outcome = entry.Outcome,
                Reason = entry.Reason,
            };
        }

        [Fact]
        public void Write_RejectsAnAction_ThatDoesNotEndWithTheOutcome()
        {
            var (writer, store, _) = CreateWriter();

            var entry = new PlatformAuditEntry
            {
                ActorKind = PlatformAuditActorKind.Admin,
                Action = "admin.login.failed",
                TargetType = "platform_admin",
                Outcome = PlatformAuditOutcome.Succeeded,
            };

            Assert.Throws<ArgumentException>(() => writer.Write(entry));
            Assert.Empty(store.Records);
        }

        [Fact]
        public void Write_RejectsAnUppercaseAnOversizedOrShortAction()
        {
            var (writer, store, _) = CreateWriter();
            var entry = CreateEntry(PlatformAuditActorKind.System);

            Assert.Throws<ArgumentException>(() => writer.Write(WithAction(entry, "Admin.Login.Succeeded")));
            Assert.Throws<ArgumentException>(() => writer.Write(WithAction(entry, new string('a', 129) + ".x.succeeded")));
            Assert.Throws<ArgumentException>(() => writer.Write(WithAction(entry, "admin.login")));
            Assert.Empty(store.Records);
        }

        [Fact]
        public void Write_RejectsAReason_ThatIsNotAShortMachineCode()
        {
            var (writer, store, _) = CreateWriter();
            var entry = CreateEntry(PlatformAuditActorKind.System);

            Assert.Throws<ArgumentException>(() => writer.Write(WithReason(entry, "the user typed a long free text reason")));
            Assert.Throws<ArgumentException>(() => writer.Write(WithReason(entry, new string('r', 65))));
            Assert.Empty(store.Records);
        }

        [Fact]
        public void Write_AcceptsADocumentedReasonCode_AndRejectsAnOversizedSubjectType()
        {
            var (writer, store, _) = CreateWriter();
            var entry = CreateEntry(PlatformAuditActorKind.System);

            writer.Write(WithReason(entry, "lockout_active"));

            Assert.Single(store.Records);
            Assert.Throws<ArgumentException>(() => writer.Write(WithTargetType(entry, new string('s', 65))));
            Assert.Single(store.Records);
        }
    }
    internal sealed class FakeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated { get; set; }

        public Guid? AdminId { get; set; }

        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    }

    internal sealed class FakeAuditStore : IPlatformAuditRecordStore
    {
        private readonly List<PlatformAuditRecord> pending = new();
        private readonly List<PlatformAuditRecord> persisted = new();

        public IReadOnlyList<PlatformAuditRecord> Records => pending;

        public IReadOnlyList<PlatformAuditRecord> PersistedRecords => persisted;

        public void Add(PlatformAuditRecord record)
        {
            pending.Add(record);
        }

        public void CommitPending()
        {
            persisted.AddRange(pending);
            pending.Clear();
        }
    }

    internal sealed class FakeAuditUnitOfWork : IMasterUnitOfWork
    {
        public Action? OnCommit { get; set; }

        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            OnCommit?.Invoke();
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }

}