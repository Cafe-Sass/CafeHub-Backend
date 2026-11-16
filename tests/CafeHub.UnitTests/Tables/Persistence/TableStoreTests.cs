using System.Data;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Tables;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Tables.Persistence
{
    public sealed class TableStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid TableId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        [Fact]
        public async Task Create_OnAnActiveBranch_StartsAtOneAndAuditsTheCreate()
        {
            var (stub, audit) = CreateStub(CreateBranch(), maxNumber: null);
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.AddAsync(BranchId, "  Terrace 3  ", ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.Number);
            Assert.Equal("Terrace 3", result.Value.Label);
            Assert.Equal("Abcdefghijklmnopqrstuv", result.Value.QrCode);
            var added = Assert.Single(context.BranchTables.Local);
            Assert.Equal(EntityState.Added, context.Entry(added).State);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("tables.created.succeeded", entry.Action);
            Assert.Equal("branch_tables", entry.TargetType);
            Assert.Equal(ActorId, entry.ActorUserId);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains("\"Number\":\"1\"", snapshot.AfterState);
            Assert.Contains("\"Status\":\"Active\"", snapshot.AfterState);
            Assert.Contains("\"BranchId\"", snapshot.AfterState);
            Assert.DoesNotContain("qr_code", snapshot.AfterState);
        }

        [Fact]
        public async Task Create_ContinuesAfterTheHighestActiveNumber()
        {
            var (stub, audit) = CreateStub(CreateBranch(), maxNumber: 7);
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.AddAsync(BranchId, null, ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(8, result.Value.Number);
        }

        [Fact]
        public async Task Create_OnANonActiveBranch_RefusesWithoutWriting()
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => TenantCommandStub.EntityRow(command, eventData, CreateBranch(BranchLifecycleStatus.Suspended)),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.AddAsync(BranchId, null, ActorId, default);

            Assert.Equal(TablesErrors.BranchNotActive(), result.Error);
            Assert.Empty(context.BranchTables.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Create_ForAMissingBranch_ReturnsBranchNotFound()
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => Empty(TenantCommandStub.EntityRow(command, eventData, CreateBranch())),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.AddAsync(BranchId, null, ActorId, default);

            Assert.Equal(TablesErrors.BranchNotFound(), result.Error);
            Assert.Empty(context.BranchTables.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task AddBulk_AddsConsecutiveNumbersAndAuditsTheRange()
        {
            var (stub, audit) = CreateStub(CreateBranch(), maxNumber: 3, activeCount: 0);
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(
                context,
                audit,
                new FixedQrGenerator(
                    "Abcdefghijklmnopqrstuv",
                    "Bbcdefghijklmnopqrstuv",
                    "Cbcdefghijklmnopqrstuv"),
                new FixedTimeProvider(Now));

            var result = await store.AddBulkAsync(BranchId, 3, ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Count);
            Assert.Equal(4, result.Value.FromNumber);
            Assert.Equal(6, result.Value.ToNumber);
            Assert.Equal(3, context.BranchTables.Local.Count);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("tables.created.succeeded", entry.Action);
            Assert.Equal("branch_tables", entry.TargetType);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains("\"count\":\"3\"", snapshot.AfterState);
            Assert.Contains("\"fromNumber\":\"4\"", snapshot.AfterState);
            Assert.Contains("\"toNumber\":\"6\"", snapshot.AfterState);
        }

        [Fact]
        public async Task AddBulk_WhenTheRangeExceeds9999_Refuses()
        {
            var (stub, audit) = CreateStub(CreateBranch(), maxNumber: 9998, activeCount: 0);
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.AddBulkAsync(BranchId, 3, ActorId, default);

            Assert.IsType<ValidationError>(result.Error);
            Assert.Empty(context.BranchTables.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task DeactivateHighest_RejectsNGreaterThanTheActiveCount()
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) =>
                {
                    var sql = command.CommandText;
                    if (sql.Contains("count", StringComparison.OrdinalIgnoreCase)
                        && sql.Contains("branch_tables", StringComparison.OrdinalIgnoreCase))
                    {
                        return TenantCommandStub.Scalars(2);
                    }

                    return TenantCommandStub.EntityRow(command, eventData, CreateBranch());
                },
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.DeactivateHighestAsync(BranchId, 5, ActorId, default);

            Assert.IsType<ValidationError>(result.Error);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task UpdateLabel_ChangesTheLabelAndAuditsTheChange()
        {
            var table = CreateTable(TableId, 1, BranchTableStatus.Active);
            var (stub, audit) = TableStub(table, version: 7);
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.UpdateLabelAsync(table.Id, null, "Counter 7", "7", ActorId, default);

            Assert.True(result.IsSuccess);
            Assert.Equal("Counter 7", result.Value.Label);
            var tracked = Assert.Single(context.BranchTables.Local);
            Assert.Equal("Counter 7", tracked.Label);
            Assert.Equal(ActorId, tracked.UpdatedBy);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("tables.updated.succeeded", entry.Action);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains("\"Label\":\"Counter 7\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Reactivate_WhenAnActiveTableUsesTheNumber_Refuses()
        {
            var inactive = CreateTable(TableId, 3, BranchTableStatus.Inactive);
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) =>
                {
                    var sql = command.CommandText;
                    if (sql.Contains("order by", StringComparison.OrdinalIgnoreCase))
                    {
                        return Rows(inactive);
                    }

                    if (sql.Contains("exists", StringComparison.OrdinalIgnoreCase))
                    {
                        return TenantCommandStub.Scalars(true);
                    }

                    return TenantCommandStub.EntityRow(command, eventData, inactive, 7);
                },
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.ReactivateAsync(inactive.Id, null, "7", ActorId, default);

            Assert.Equal(TablesErrors.NumberConflict(), result.Error);
            Assert.Equal(BranchTableStatus.Inactive, Assert.Single(context.BranchTables.Local).Status);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Update_WithAStaleToken_ReturnsAConcurrencyConflict()
        {
            var table = CreateTable(TableId, 1, BranchTableStatus.Active);
            var (stub, audit) = TableStub(table, version: 7);
            await using var context = stub.CreateContext(new TableSaveStub());
            var store = new TableStore(context, audit, new FixedQrGenerator("Abcdefghijklmnopqrstuv"), new FixedTimeProvider(Now));

            var result = await store.UpdateLabelAsync(table.Id, null, "New Label", "6", ActorId, default);

            Assert.Equal(TablesErrors.ConcurrencyConflict(), result.Error);
            Assert.Empty(audit.Entries);
        }

        private static (TenantCommandStub Stub, RecordingTenantAuditWriter Audit) CreateStub(
            Branch branch,
            int? maxNumber,
            int activeCount = 0)
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) =>
                {
                    var sql = command.CommandText;
                    if (sql.StartsWith("select max", StringComparison.OrdinalIgnoreCase))
                    {
                        return maxNumber is null
                            ? ScalarNull()
                            : TenantCommandStub.Scalars(maxNumber.Value);
                    }

                    if (sql.Contains("count", StringComparison.OrdinalIgnoreCase)
                        && sql.Contains("branch_tables", StringComparison.OrdinalIgnoreCase))
                    {
                        return TenantCommandStub.Scalars(activeCount);
                    }

                    return TenantCommandStub.EntityRow(command, eventData, branch);
                },
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            return (stub, audit);
        }

        private static (TenantCommandStub Stub, RecordingTenantAuditWriter Audit) TableStub(
            BranchTable table,
            uint version)
        {
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub(
                (command, eventData) => TenantCommandStub.EntityRow(command, eventData, table, version),
                command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            return (stub, audit);
        }

        private static DataTable Rows(params BranchTable[] tables)
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("id", typeof(Guid));
            dataTable.Columns.Add("branch_id", typeof(Guid));
            dataTable.Columns.Add("number", typeof(int));
            dataTable.Columns.Add("label", typeof(string));
            dataTable.Columns.Add("qr_code", typeof(string));
            dataTable.Columns.Add("status", typeof(string));
            dataTable.Columns.Add("created_at", typeof(DateTimeOffset));
            dataTable.Columns.Add("updated_at", typeof(DateTimeOffset));
            dataTable.Columns.Add("created_by", typeof(Guid));
            dataTable.Columns.Add("updated_by", typeof(Guid));
            dataTable.Columns.Add("xmin", typeof(uint));
            foreach (var t in tables)
            {
                dataTable.Rows.Add(t.Id, t.BranchId, t.Number, t.Label, t.QrCode, t.Status, t.CreatedAt, t.UpdatedAt, t.CreatedBy, t.UpdatedBy, 1u);
            }

            return dataTable;
        }

        private static DataTable Empty(DataTable dataTable)
        {
            dataTable.Rows.Clear();
            return dataTable;
        }

        private static DataTable ScalarNull()
        {
            var table = new DataTable();
            table.Columns.Add("value", typeof(int));
            table.Rows.Add(DBNull.Value);
            return table;
        }

        private static Branch CreateBranch(BranchLifecycleStatus status = BranchLifecycleStatus.Active) =>
            Branch.Create(BranchId, "Downtown", status, null, null, null, Now, ActorId);

        private static BranchTable CreateTable(Guid id, int number, BranchTableStatus status)
        {
            var table = BranchTable.Create(id, BranchId, number, null, "Abcdefghijklmnopqrstuv", Now, ActorId);
            if (status == BranchTableStatus.Inactive)
            {
                table.Deactivate(Now.AddMinutes(5), ActorId);
            }

            return table;
        }

        private sealed class FixedQrGenerator(params string[] codes) : ITableQrCodeGenerator
        {
            private int index;

            public string Generate() => codes[index++ % codes.Length];
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class TableSaveStub : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                foreach (var entry in eventData.Context!.ChangeTracker.Entries<BranchTable>())
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        entry.Property<uint>("xmin").CurrentValue = 8;
                    }
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }
    }
}
