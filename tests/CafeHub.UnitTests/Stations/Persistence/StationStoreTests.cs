using System.Data;
using System.Data.Common;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Stations;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Stations;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Stations.Persistence
{
    public sealed class StationStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [Fact]
        public async Task Create_OnAnActiveBranch_AddsTheStationAndAuditsTheCreate()
        {
            var (stub, audit) = CreateStub(CreateBranch());
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync(BranchId, "  Bar  ", 3, ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, result.Value.BranchId);
            Assert.Equal("Bar", result.Value.Name);
            Assert.Equal(3, result.Value.SortOrder);
            var added = Assert.Single(context.Stations.Local);
            Assert.Equal(EntityState.Added, context.Entry(added).State);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("stations.created.succeeded", entry.Action);
            Assert.Equal("stations", entry.TargetType);
            Assert.Equal(added.Id, entry.TargetId);
            Assert.Equal(ActorId, entry.ActorUserId);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Null(snapshot.BeforeState);
            Assert.Contains("\"Name\":\"Bar\"", snapshot.AfterState);
            Assert.Contains("\"Status\":\"Active\"", snapshot.AfterState);
            Assert.Contains("\"BranchId\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Create_OnANonActiveBranch_RefusesWithoutWriting()
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => TenantCommandStub.EntityRow(
                    command,
                    eventData,
                    CreateBranch(BranchLifecycleStatus.Suspended)),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync(BranchId, "Bar", 0, ActorId, default);

            Assert.Equal(StationsErrors.BranchNotActive(), result.Error);
            Assert.Empty(context.Stations.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Create_ForAMissingBranch_ReturnsBranchNotFound()
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => Empty(TenantCommandStub.EntityRow(command, eventData, CreateBranch())),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync(BranchId, "Bar", 0, ActorId, default);

            Assert.Equal(StationsErrors.BranchNotFound(), result.Error);
            Assert.Empty(context.Stations.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Create_WithAnExistingActiveName_RefusesCaseInsensitively()
        {
            var audit = new RecordingTenantAuditWriter();
            string? existsSql = null;
            var stub = new TenantCommandStub(
                (command, eventData) =>
                {
                    if (command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal))
                    {
                        existsSql = command.CommandText;
                        return TenantCommandStub.Scalars(true);
                    }

                    return TenantCommandStub.EntityRow(command, eventData, CreateBranch());
                },
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync(BranchId, "bar", 0, ActorId, default);

            Assert.Equal(StationsErrors.NameConflict(), result.Error);
            Assert.NotNull(existsSql);
            Assert.Contains("lower(", existsSql, StringComparison.Ordinal);
            Assert.Empty(context.Stations.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Update_RenamesAndReordersAnActiveStationWithAudit()
        {
            var station = CreateStation();
            var (stub, audit) = StationStub(station, 7);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.UpdateAsync(station.Id, null, "Barista", 4, "7", ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal("8", result.Value.ConcurrencyToken);
            var tracked = Assert.Single(context.Stations.Local);
            Assert.Equal("Barista", tracked.Name);
            Assert.Equal(4, tracked.SortOrder);
            Assert.Equal(ActorId, tracked.UpdatedBy);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("stations.updated.succeeded", entry.Action);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains("\"Name\":\"Bar\"", snapshot.BeforeState);
            Assert.Contains("\"Name\":\"Barista\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Update_WithAnExistingActiveName_RefusesWithoutChangingTheStation()
        {
            var station = CreateStation();
            var (stub, audit) = StationStub(station, 7, nameExists: true);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.UpdateAsync(station.Id, null, "Kitchen", 4, "7", ActorId, default);

            Assert.Equal(StationsErrors.NameConflict(), result.Error);
            var tracked = Assert.Single(context.Stations.Local);
            Assert.Equal("Bar", tracked.Name);
            Assert.Equal(0, tracked.SortOrder);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Update_OfAnInactiveStation_DoesNotCheckTheActiveNameIndex()
        {
            var station = CreateStation();
            station.Deactivate(Now, ActorId);
            var (stub, audit) = StationStub(station, 7);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.UpdateAsync(station.Id, null, "Kitchen", 4, "7", ActorId, default);

            Assert.True(result.IsSuccess);
            var tracked = Assert.Single(context.Stations.Local);
            Assert.Equal("Kitchen", tracked.Name);
            Assert.Equal(StationStatus.Inactive, tracked.Status);
        }

        [Fact]
        public async Task Update_WithAMalformedToken_IsRejectedBeforeTheBranchLock()
        {
            var station = CreateStation();
            var locks = 0;
            var stub = new TenantCommandStub(
                (command, eventData) => TenantCommandStub.EntityRow(command, eventData, station, 7),
                _ => locks++);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, new RecordingTenantAuditWriter(), new FixedTimeProvider(Now));

            var result = await store.UpdateAsync(station.Id, null, "Kitchen", 4, "not-a-version", ActorId, default);

            Assert.IsType<ValidationError>(result.Error);
            Assert.Equal(0, locks);
        }

        [Fact]
        public async Task Deactivate_WithAStaleToken_ReturnsAConcurrencyConflict()
        {
            var station = CreateStation();
            var (stub, audit) = StationStub(station, 7);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.DeactivateAsync(station.Id, null, "6", ActorId, default);

            Assert.Equal(StationsErrors.ConcurrencyConflict(), result.Error);
            Assert.Equal(StationStatus.Active, Assert.Single(context.Stations.Local).Status);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Deactivate_RecordsTheLifecycleChangeAndTheAudit()
        {
            var station = CreateStation();
            var (stub, audit) = StationStub(station, 7);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.DeactivateAsync(station.Id, null, "7", ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal("Inactive", result.Value.Status);
            Assert.Equal(StationStatus.Inactive, Assert.Single(context.Stations.Local).Status);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("stations.deactivated.succeeded", entry.Action);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains("\"Status\":\"Active\"", snapshot.BeforeState);
            Assert.Contains("\"Status\":\"Inactive\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Reactivate_WhenAnActiveStationUsesTheName_RefusesAndKeepsTheStatus()
        {
            var station = CreateStation();
            station.Deactivate(Now, ActorId);
            var (stub, audit) = StationStub(station, 7, nameExists: true);
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ReactivateAsync(station.Id, null, "7", ActorId, default);

            Assert.Equal(StationsErrors.NameConflict(), result.Error);
            Assert.Equal(StationStatus.Inactive, Assert.Single(context.Stations.Local).Status);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task GetById_ReturnsTheRowVersionForTheRequestedScope()
        {
            var station = CreateStation();
            string? sql = null;
            var stub = new TenantCommandStub((command, eventData) =>
            {
                sql = command.CommandText;
                return TenantCommandStub.EntityRow(command, eventData, station, 7);
            });
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, new RecordingTenantAuditWriter(), new FixedTimeProvider(Now));

            var result = await store.GetByIdAsync(station.Id, BranchId, default);

            Assert.NotNull(result);
            Assert.Equal("7", result.ConcurrencyToken);
            Assert.Contains("branch_id", sql, StringComparison.Ordinal);
        }

        [Fact]
        public async Task List_OrdersDeterministicallyAndFetchesOneExtraRow()
        {
            var station = CreateStation();
            var counted = false;
            var stub = new TenantCommandStub((command, eventData) =>
            {
                if (!counted)
                {
                    counted = true;
                    Assert.Contains("count(", command.CommandText, StringComparison.Ordinal);
                    return TenantCommandStub.Scalars(5);
                }

                var sql = command.CommandText;
                var orderBy = sql.IndexOf("ORDER BY", StringComparison.Ordinal);
                Assert.True(orderBy >= 0, sql);
                var ordering = sql[orderBy..];
                Assert.True(ordering.IndexOf("sort_order", StringComparison.Ordinal)
                    < ordering.IndexOf("name", StringComparison.Ordinal), ordering);
                Assert.True(ordering.IndexOf("name", StringComparison.Ordinal)
                    < ordering.IndexOf("id", StringComparison.Ordinal), ordering);
                var values = command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).ToArray();
                Assert.Contains(3, values);
                Assert.Contains(2, values);
                return TenantCommandStub.EntityRow(command, eventData, station, 7);
            });
            await using var context = stub.CreateContext(new StationSaveStub());
            var store = new StationStore(context, new RecordingTenantAuditWriter(), new FixedTimeProvider(Now));

            var page = await store.ListAsync(
                new StationListRequest(BranchId, Page: 2, PageSize: 2, IncludeTotalCount: true, StationStatus.Active),
                default);

            Assert.Equal(5, page.TotalCount);
            Assert.Equal("7", Assert.Single(page.FetchedItems).ConcurrencyToken);
        }

        private static (TenantCommandStub Stub, RecordingTenantAuditWriter Audit) CreateStub(
            Branch branch,
            bool nameExists = false)
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal)
                    ? TenantCommandStub.Scalars(nameExists)
                    : TenantCommandStub.EntityRow(command, eventData, branch),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            return (stub, audit);
        }

        private static (TenantCommandStub Stub, RecordingTenantAuditWriter Audit) StationStub(
            Station station,
            uint version,
            bool nameExists = false)
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal)
                    ? TenantCommandStub.Scalars(nameExists)
                    : TenantCommandStub.EntityRow(command, eventData, station, version),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            return (stub, audit);
        }

        private static Station CreateStation() =>
            Station.Create(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), BranchId, "Bar", 0, Now, ActorId);

        private static Branch CreateBranch(BranchLifecycleStatus status = BranchLifecycleStatus.Active) =>
            Branch.Create(BranchId, "Downtown", status, null, null, null, Now, ActorId);

        private static DataTable Empty(DataTable table)
        {
            table.Rows.Clear();
            return table;
        }

        private sealed class StationSaveStub : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                foreach (var entry in eventData.Context!.ChangeTracker.Entries<Station>())
                {
                    if (entry.State == EntityState.Modified)
                    {
                        entry.Property<uint>("xmin").CurrentValue = 8;
                    }
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }
    }
}
