using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Menu;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CafeHub.UnitTests.Menu
{
    public sealed class MenuCategoryStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();
        private const uint Version = 13;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Create_StagesAValidCategoryAndChangedFieldAudit(bool isChild)
        {
            var parent = isChild ? Create("Drinks") : null;
            var locks = new List<Guid>();
            var stub = new TenantCommandStub(
                (command, data) => TenantCommandStub.EntityRow(command, data, parent!, Version),
                command => locks.Add(ReadLock(command)));
            var audit = new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new MenuCategoryStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync(parent?.Id, "  Coffee  ", 3, Actor, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(parent?.Id, result.Value.ParentId);
            Assert.Equal(isChild ? [parent!.Id] : Array.Empty<Guid>(), locks);
            Assert.Equal("Coffee", result.Value.Name);
            Assert.Equal("Active", result.Value.Status);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("menu_categories.created.succeeded", entry.Action);
            Assert.Equal(result.Value.Id, entry.TargetId);
            Assert.Equal(Actor, entry.ActorUserId);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Null(snapshot.BeforeState);
            Assert.Contains("\"SortOrder\":\"3\"", snapshot.AfterState);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("inactive")]
        [InlineData("child")]
        public async Task Create_RejectsAllInvalidParentsWithTheSameValidationError(string kind)
        {
            var root = Create("Root");
            var parent = kind == "child" ? Create("Child", root) : root;
            if (kind == "inactive")
            {
                parent.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            }

            var stub = new TenantCommandStub(
                (command, data) => kind == "missing"
                    ? new DataTable()
                    : TenantCommandStub.EntityRow(command, data, parent, Version),
                command => Assert.Equal(parent.Id, ReadLock(command)));
            var audit = new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter();
            await using var context = stub.CreateContext();
            var store = new MenuCategoryStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync(parent.Id, "New", 0, Actor, default);

            var error = Assert.IsType<ValidationError>(result.Error);
            Assert.Equal("parentId", Assert.Single(error.Errors).Field);
            Assert.Empty(audit.Entries);
            Assert.Empty(context.ChangeTracker.Entries<MenuCategory>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReactivateChild_UsesTheParentStateAfterLockAcquisition(bool parentDeactivatesFirst)
        {
            var root = Create("Root");
            var child = Create("Child", root);
            child.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            var locks = new List<Guid>();
            var stub = ForCategory(child, root, id =>
            {
                locks.Add(id);
                if (id == root.Id && parentDeactivatesFirst)
                {
                    root.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
                }
            });
            var audit = new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new MenuCategoryStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ChangeStatusAsync(child.Id, MenuCategoryStatus.Active, Version, Actor, default);

            Assert.Equal(new[] { root.Id, child.Id }, locks);
            if (parentDeactivatesFirst)
            {
                Assert.Equal(MenuCategoryErrors.ParentInactive(), result.Error);
                Assert.Empty(audit.Entries);
                Assert.Equal(MenuCategoryStatus.Inactive, Assert.Single(context.MenuCategories.Local).Status);
            }
            else
            {
                Assert.True(result.IsSuccess);
                Assert.Equal("Active", result.Value.Status);
                var entry = Assert.Single(audit.Entries);
                Assert.Equal("menu_categories.reactivated.succeeded", entry.Action);
                Assert.Equal(child.Id, entry.TargetId);
                var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
                Assert.Equal("{\"Status\":\"Inactive\"}", snapshot.BeforeState);
                Assert.Equal("{\"Status\":\"Active\"}", snapshot.AfterState);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ParentLifecycle_UpdatesAndAuditsOnlyTheParent(bool reactivate)
        {
            var root = Create("Drinks");
            var child = Create("Coffee", root);
            if (reactivate)
            {
                root.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            }

            var audit = new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter();
            var stub = ForCategory(root, null, id => Assert.Equal(root.Id, id));
            await using var context = stub.CreateContext(new SaveStub());
            var store = new MenuCategoryStore(context, audit, new FixedTimeProvider(Now));
            var status = reactivate ? MenuCategoryStatus.Active : MenuCategoryStatus.Inactive;

            var result = await store.ChangeStatusAsync(root.Id, status, Version, Actor, default);

            Assert.Equal(status.ToString(), result.Value.Status);
            Assert.Equal(root.Id, Assert.Single(context.MenuCategories.Local).Id);
            Assert.Equal(root.Id, Assert.Single(audit.Entries).TargetId);
            Assert.Equal(MenuCategoryStatus.Active, child.Status);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SameStateLifecycle_ReturnsCurrentRepresentationOrStaleConflictWithoutSaving(bool stale)
        {
            var root = Create("Root");
            var child = Create("Child", root);
            root.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            var audit = new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter();
            await using var context = ForCategory(child, root, _ => { }).CreateContext();
            var store = new MenuCategoryStore(context, audit, new FixedTimeProvider(Now.AddMinutes(1)));

            var result = await store.ChangeStatusAsync(child.Id, MenuCategoryStatus.Active, stale ? Version - 1 : Version, Actor, default);

            if (stale)
            {
                Assert.Equal(MenuCategoryErrors.ConcurrencyConflict(), result.Error);
            }
            else
            {
                Assert.Equal("13", result.Value.ConcurrencyToken);
                Assert.Equal("Active", result.Value.Status);
            }

            Assert.Empty(audit.Entries);
            Assert.Equal(Now, Assert.Single(context.MenuCategories.Local).UpdatedAt);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HiddenRows_CanBeRenamedAndReorderedWithoutChangingParentOrStatus(bool inactive)
        {
            var root = Create("Root");
            var child = Create("Before", root);
            root.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            if (inactive)
            {
                child.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            }

            var audit = new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter();
            await using var context = ForCategory(child, root, _ => { }).CreateContext(new SaveStub());
            var store = new MenuCategoryStore(context, audit, new FixedTimeProvider(Now.AddMinutes(1)));
            var result = await store.UpdateAsync(child.Id, " After ", 8, Version, Actor, default);

            Assert.Equal("After", result.Value.Name);
            Assert.Equal(8, result.Value.SortOrder);
            Assert.Equal(root.Id, result.Value.ParentId);
            Assert.Equal(child.Status.ToString(), result.Value.Status);
            Assert.Equal("menu_categories.updated.succeeded", Assert.Single(audit.Entries).Action);
        }

        [Theory]
        [InlineData("uq_menu_categories_active_root_name")]
        [InlineData("uq_menu_categories_active_child_name")]
        [InlineData(null)]
        public async Task FailedSave_RollsBackWithBothCategoryAndAuditStaged(string? index)
        {
            var tenantId = Guid.NewGuid();
            var currentUser = new FakeCurrentTenantUser { IsAuthenticated = true, UserId = Actor, TenantId = tenantId };
            var tenant = new StubTenantContextAccessor(TenantContext.Create(tenantId, "fixture", true));
            var unitOfWork = new RecordingTenantUnitOfWork();
            using var services = new ServiceCollection().AddSingleton<ITenantUnitOfWork>(unitOfWork).BuildServiceProvider();

            var save = new SaveStub(context =>
            {
                Assert.Single(context.ChangeTracker.Entries<MenuCategory>());
                var audit = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Contains("\"Name\":\"CAFE\"", audit.AfterState);
                if (index is null)
                {
                    throw new DbUpdateConcurrencyException("Fixture stale write.");
                }

                throw new DbUpdateException("Fixture duplicate name.", new PostgresException(
                    "Fixture collision.", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: index));
            });
            await using var context = new TenantCommandStub((_, _) => throw new InvalidOperationException()).CreateContext(save);
            var clock = new FixedTimeProvider(Now);
            var store = new MenuCategoryStore(context, new TenantAuditWriter(currentUser, clock, context, tenant), clock);
            var command = new CreateMenuCategoryCommand(null, "CAFE", 0);
            var behavior = new TenantTransactionBehavior<CreateMenuCategoryCommand, Result<MenuCategoryResponse>>(services);
            var result = await behavior.Handle(command, token => store.CreateAsync(null, "CAFE", 0, Actor, token), default);

            Assert.Equal(index is null ? MenuCategoryErrors.ConcurrencyConflict() : MenuCategoryErrors.NameConflict(), result.Error);
            Assert.Equal(1, unitOfWork.BeginCount);
            Assert.Equal(0, unitOfWork.CommitCount);
            Assert.Equal(1, unitOfWork.RollbackCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CountedTree_UsesOneRepeatableReadTransactionAndDisposesIt(bool pageFails)
        {
            var root = Create("Drinks");
            await using var connection = new TransactionConnection();
            var reads = new List<string>();
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Same(connection.Transaction, command.Transaction);
                Assert.Equal(IsolationLevel.RepeatableRead, command.Transaction!.IsolationLevel);
                Assert.False(connection.Transaction!.Disposed);
                if (command.CommandText.StartsWith("SELECT count(", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Contains("parent_id IS NULL", command.CommandText);
                    Assert.Contains("'Active'", command.CommandText);
                    reads.Add("count");
                    return TenantCommandStub.Scalars(3);
                }

                Assert.Contains("EXISTS", command.CommandText);
                reads.Add("page");
                return pageFails
                    ? throw new InvalidOperationException("Fixture page read failed.")
                    : TreeRows(root, false, []);
            });
            await using var context = CreateTransactionalContext(stub, connection);
            var store = new MenuCategoryStore(context, new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter(), new FixedTimeProvider(Now));
            var handler = new GetMenuCategoryTreeQueryHandler(store, new PaginationOptions());
            var query = new GetMenuCategoryTreeQuery(false, new OffsetPageRequest { Page = 2, PageSize = 2, IncludeTotalCount = true });

            if (pageFails)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(query, default));
            }
            else
            {
                var result = await handler.Handle(query, default);
                var page = result.Value;
                Assert.Equal(3, page.TotalCount);
                Assert.Equal(root.Id, Assert.Single(page.Items).Id);
                Assert.False(page.HasNextPage);
            }

            Assert.Equal(new[] { "count", "page" }, reads);
            Assert.Equal(!pageFails, connection.Transaction!.Committed);
            Assert.True(connection.Transaction.Disposed);
            Assert.Null(context.Database.CurrentTransaction);
        }

        [Fact]
        public async Task ReactivateChild_WhenAnActiveSiblingReservesTheName_RollsBackTheUpdateAndAudit()
        {
            var root = Create("Drinks");
            var child = Create("Coffee", root);
            child.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);
            var sibling = Create("COFFEE", root);
            await using var connection = new TransactionConnection();
            var persistedStatus = child.Status;
            var committedAudits = new List<Guid>();
            var tenantId = Guid.NewGuid();
            var currentUser = new FakeCurrentTenantUser { IsAuthenticated = true, UserId = Actor, TenantId = tenantId };
            var tenant = new StubTenantContextAccessor(TenantContext.Create(tenantId, "fixture", true));
            var stub = new TenantCommandStub(
                (command, data) =>
                {
                    Assert.Same(connection.Transaction, command.Transaction);
                    if (command.CommandText.Contains("UPDATE menu_categories", StringComparison.Ordinal))
                    {
                        Assert.Contains("xmin", command.CommandText);
                        Assert.Contains("INSERT INTO tenant_audit_records", command.CommandText);
                        var pending = Assert.Single(data.Context!.ChangeTracker.Entries<MenuCategory>()).Entity;
                        var audit = Assert.Single(data.Context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                        Assert.Equal(child.Id, pending.Id);
                        Assert.Equal(MenuCategoryStatus.Active, pending.Status);
                        Assert.Equal(sibling.ParentId, pending.ParentId);
                        Assert.Equal(sibling.Name, pending.Name, ignoreCase: true);
                        Assert.Equal(MenuCategoryStatus.Active, sibling.Status);
                        Assert.Equal("{\"Status\":\"Inactive\"}", audit.BeforeState);
                        Assert.Equal("{\"Status\":\"Active\"}", audit.AfterState);
                        connection.Transaction!.Stage(() => persistedStatus = pending.Status);
                        connection.Transaction.Stage(() => committedAudits.Add(audit.Id));
                        throw new PostgresException("Fixture active sibling conflict.", "ERROR", "ERROR",
                            PostgresErrorCodes.UniqueViolation, constraintName: "uq_menu_categories_active_child_name");
                    }

                    return ReadCategory(command, data, child, root);
                },
                command =>
                {
                    Assert.Same(connection.Transaction, command.Transaction);
                    ReadLock(command);
                });
            await using var context = CreateTransactionalContext(stub, connection);
            using var services = new ServiceCollection().AddSingleton<ITenantUnitOfWork>(new TenantUnitOfWork(context)).BuildServiceProvider();
            var clock = new FixedTimeProvider(Now);
            var store = new MenuCategoryStore(context, new TenantAuditWriter(currentUser, clock, context, tenant), clock);
            var handler = new ReactivateMenuCategoryCommandHandler(store, currentUser, tenant);
            var command = new ReactivateMenuCategoryCommand(child.Id, "13");
            var behavior = new TenantTransactionBehavior<ReactivateMenuCategoryCommand, Result<MenuCategoryResponse>>(services);

            var result = await behavior.Handle(command, token => handler.Handle(command, token), default);

            Assert.Equal(MenuCategoryErrors.NameConflict(), result.Error);
            Assert.Equal(MenuCategoryStatus.Inactive, persistedStatus);
            Assert.Empty(committedAudits);
            Assert.True(connection.Transaction!.SavepointRolledBack);
            Assert.True(connection.Transaction.RolledBack);
            Assert.False(connection.Transaction.Committed);
            Assert.True(connection.Transaction.Disposed);
            Assert.Null(context.Database.CurrentTransaction);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Tree_PagesOrderedRootsBeforeJoiningOrderedChildrenInOneStatement(bool includeInactive)
        {
            var reads = 0;
            var stub = new TenantCommandStub((command, _) =>
            {
                ++reads;
                Assert.Null(command.Transaction);
                var sql = command.CommandText;
                Assert.Contains("parent_id IS NULL", sql, StringComparison.Ordinal);
                Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
                Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
                Assert.Contains("OFFSET", sql, StringComparison.Ordinal);
                Assert.Contains("EXISTS", sql, StringComparison.Ordinal);
                Assert.Matches(@"ORDER BY \w+\.sort_order, \w+\.id", sql);
                Assert.Matches(@"ORDER BY \w+\.sort_order, \w+\.id, \w+\.sort_order, \w+\.id", sql);
                Assert.Equal(!includeInactive, sql.Contains("'Active'", StringComparison.Ordinal));
                Assert.Contains(command.Parameters.Cast<DbParameter>(), param => Equals(param.Value, 5));
                return new DataTable();
            });
            await using var context = stub.CreateContext();
            var store = new MenuCategoryStore(context, new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter(), new FixedTimeProvider(Now));

            var page = await store.GetTreeAsync(includeInactive, 2, 5, false, default);

            Assert.Equal(1, reads);
            Assert.Empty(page.Items);
            Assert.Equal(2, page.Page);
            Assert.Equal(5, page.PageSize);
            Assert.Null(page.TotalCount);
            Assert.False(page.HasNextPage);
        }

        [Fact]
        public async Task Tree_MaterializesOnlyChildrenOfReturnedRoots()
        {
            var root = Create("Drinks");
            var children = new[] { Create("Tea", root), Create("Coffee", root) }.OrderBy(child => child.Id).ToArray();
            var lookaheadRoot = MenuCategory.Create(Guid.NewGuid(), null, "Food", 1, Now, Actor);
            var roots = new[] { root, lookaheadRoot };
            var allChildren = children.Concat(Enumerable.Range(0, 200).Select(index => Create($"Item {index}", lookaheadRoot))).ToArray();
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Null(command.Transaction);
                var sql = command.CommandText;
                var exists = System.Text.RegularExpressions.Regex.Match(sql, @"EXISTS \((?<query>[\s\S]+?)\) AS \w+\r?\n\s*FROM menu_categories AS");
                Assert.True(exists.Success, sql);
                Assert.Contains("parent_id IS NULL", exists.Groups["query"].Value);
                Assert.Contains("LIMIT 2 OFFSET 0", exists.Groups["query"].Value);
                Assert.EndsWith("OFFSET 1", exists.Groups["query"].Value);
                Assert.DoesNotContain("JOIN", exists.Groups["query"].Value);
                var join = sql.LastIndexOf("LEFT JOIN", StringComparison.Ordinal);
                Assert.True(join >= 0, sql);
                var limit = System.Text.RegularExpressions.Regex.Match(sql[..join], @"LIMIT (@\w+)\s+(?:OFFSET @\w+\s+)?\) AS \w+\s*$");
                Assert.True(limit.Success, sql);
                var joinLimit = Assert.Single(command.Parameters.Cast<DbParameter>(),
                    parameter => parameter.ParameterName.TrimStart('@') == limit.Groups[1].Value.TrimStart('@'));
                var limitValue = Assert.IsType<int>(joinLimit.Value);
                Assert.Equal(1, limitValue);
                Assert.Matches(@"ON \w+\.id = \w+\.parent_id", sql[join..]);
                var returnedRoot = Assert.Single(roots.Take(limitValue));
                var joinedChildren = allChildren.Where(child => child.ParentId == returnedRoot.Id).ToArray();
                Assert.Equal(2, joinedChildren.Length);
                return TreeRows(returnedRoot, roots.Length > limitValue, joinedChildren);
            });
            await using var context = stub.CreateContext();
            var store = new MenuCategoryStore(context, new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter(), new FixedTimeProvider(Now));

            var page = await store.GetTreeAsync(false, 1, 1, false, default);

            var first = Assert.Single(page.Items);
            Assert.Equal(root.Id, first.Id);
            Assert.True(page.HasNextPage);
            Assert.Equal(children.Select(child => child.Id), first.Children.Select(child => child.Id));
            Assert.All(first.Children, child => Assert.Equal(root.Id, child.ParentId));
            Assert.DoesNotContain(first.Children, child => child.ParentId == lookaheadRoot.Id);
        }

        [Fact]
        public async Task DirectId_RequiresTheRowAndItsParentToBeVisible()
        {
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Contains("LEFT JOIN", command.CommandText, StringComparison.Ordinal);
                Assert.Matches(@"\w+\.status = 'Active' AND \(\w+\.parent_id IS NULL OR \w+\.status = 'Active'\)", command.CommandText);
                return new DataTable();
            });
            await using var context = stub.CreateContext();
            var store = new MenuCategoryStore(context, new CafeHub.UnitTests.Branches.RecordingTenantAuditWriter(), new FixedTimeProvider(Now));
            Assert.Null(await store.GetVisibleAsync(Guid.NewGuid(), default));
        }

        private static TenantCommandStub ForCategory(MenuCategory target, MenuCategory? parent, Action<Guid> onLock) =>
            new(
                (command, data) => ReadCategory(command, data, target, parent),
                command => onLock(ReadLock(command)));

        private static DataTable ReadCategory(DbCommand command, CommandEventData data, MenuCategory target, MenuCategory? parent)
        {
            if (command.CommandText.StartsWith("SELECT m.parent_id AS", StringComparison.Ordinal))
            {
                var row = new DataTable();
                row.Columns.Add("parent_id", typeof(Guid));
                row.Rows.Add((object?)target.ParentId ?? DBNull.Value);
                return row;
            }

            var id = Assert.IsType<Guid>(Assert.Single(command.Parameters.Cast<DbParameter>()).Value);
            return TenantCommandStub.EntityRow(command, data, id == target.Id ? target : parent!, Version);
        }

        private static Guid ReadLock(DbCommand command)
        {
            Assert.Contains("FROM menu_categories", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal);
            return Assert.IsType<Guid>(Assert.Single(command.Parameters.Cast<DbParameter>()).Value);
        }

        private static MenuCategory Create(string name, MenuCategory? parent = null) =>
            MenuCategory.Create(Guid.CreateVersion7(), parent, name, 0, Now, Actor);

        private static object?[] CategoryValues(MenuCategory category) =>
            [category.Id, category.CreatedAt, category.CreatedBy, category.Name, category.ParentId,
                category.SortOrder, category.Status.ToString(), category.UpdatedAt, category.UpdatedBy, Version];

        private static DataTable TreeRows(MenuCategory root, bool hasNext, IReadOnlyList<MenuCategory> children)
        {
            var rows = new DataTable();
            AddColumns("root");
            rows.Columns.Add("has_next", typeof(bool));
            AddColumns("child");
            if (children.Count == 0)
            {
                rows.Rows.Add([.. CategoryValues(root), hasNext, .. new object?[10]]);
            }
            else
            {
                foreach (var child in children)
                {
                    rows.Rows.Add([.. CategoryValues(root), hasNext, .. CategoryValues(child)]);
                }
            }

            return rows;

            void AddColumns(string prefix)
            {
                rows.Columns.Add(prefix + "Id", typeof(Guid));
                rows.Columns.Add(prefix + "CreatedAt", typeof(DateTimeOffset));
                rows.Columns.Add(prefix + "CreatedBy", typeof(Guid));
                rows.Columns.Add(prefix + "Name", typeof(string));
                rows.Columns.Add(prefix + "ParentId", typeof(Guid));
                rows.Columns.Add(prefix + "SortOrder", typeof(int));
                rows.Columns.Add(prefix + "Status", typeof(string));
                rows.Columns.Add(prefix + "UpdatedAt", typeof(DateTimeOffset));
                rows.Columns.Add(prefix + "UpdatedBy", typeof(Guid));
                rows.Columns.Add(prefix + "Version", typeof(uint));
            }
        }

        private static TenantDbContext CreateTransactionalContext(TenantCommandStub stub, TransactionConnection connection) =>
            new(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connection).AddInterceptors(stub).Options);

        private sealed class TransactionConnection : DbConnection
        {
            private ConnectionState state;

            public RecordingTransaction? Transaction { get; private set; }
            [AllowNull]
            public override string ConnectionString { get; set; } = "Host=192.0.2.1;Database=unit-test";
            public override string Database => "unit-test";
            public override string DataSource => "offline";
            public override string ServerVersion => "18.6";
            public override ConnectionState State => state;

            public override void Open() => state = ConnectionState.Open;

            public override void Close() => state = ConnectionState.Closed;

            public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                Assert.Equal(ConnectionState.Open, State);
                Assert.Null(Transaction);
                return Transaction = new RecordingTransaction(this, isolationLevel);
            }

            protected override DbCommand CreateDbCommand()
            {
                // EF generates Npgsql parameters, and the interceptor supplies every execution result.
                DbCommand command = new TransactionCommand();
                command.Connection = this;
                return command;
            }
        }

        private sealed class TransactionCommand : NpgsqlCommand
        {
            protected override DbConnection? DbConnection { get; set; }
            protected override DbTransaction? DbTransaction { get; set; }

            public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
            {
                var transaction = Assert.IsType<RecordingTransaction>(DbTransaction);
                if (CommandText.StartsWith("SAVEPOINT ", StringComparison.Ordinal))
                {
                    transaction.Savepoint();
                }
                else
                {
                    Assert.StartsWith("ROLLBACK TO ", CommandText);
                    transaction.RollbackToSavepoint();
                }

                return Task.FromResult(0);
            }
        }

        private sealed class RecordingTransaction(DbConnection connection, IsolationLevel isolationLevel) : DbTransaction
        {
            private readonly List<Action> pending = [];
            private int savepoint;
            public override IsolationLevel IsolationLevel => isolationLevel;
            protected override DbConnection DbConnection => connection;
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }
            public bool Disposed { get; private set; }
            public bool SavepointRolledBack { get; private set; }

            public void Stage(Action persist) => pending.Add(persist);

            public void Savepoint() => savepoint = pending.Count;

            public void RollbackToSavepoint()
            {
                pending.RemoveRange(savepoint, pending.Count - savepoint);
                SavepointRolledBack = true;
            }

            public override void Commit()
            {
                Assert.False(Disposed || RolledBack);
                foreach (var persist in pending)
                {
                    persist();
                }

                Committed = true;
            }

            public override void Rollback()
            {
                Assert.False(Disposed || Committed);
                pending.Clear();
                RolledBack = true;
            }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }

        private sealed class SaveStub(Action<DbContext>? check = null) : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                check?.Invoke(eventData.Context!);
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }
    }
}
