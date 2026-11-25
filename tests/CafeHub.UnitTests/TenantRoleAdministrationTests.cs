using System.Data;
using System.Data.Common;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Roles;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests
{
    public sealed class TenantRoleAdministrationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void EffectiveGrantsUseAnAdditiveUnionAndIgnoreReservedAndUnknownCodes()
        {
            var fromRoles = new[] { TenantPermissions.StaffRead, TenantPermissions.BranchesRead };
            var personal = new[] { TenantPermissions.StaffManage, TenantPermissions.StaffRead, TenantPermissions.RolesManage, "unknown" };

            Assert.Equal(new[] { "bills.close", "bills.view", "branches.manage", "branches.read", "cafe-profile.manage", "guests.mute", "guests.mute.audit", "inventory.manage", "menu.availability", "menu.manage", "orders.captain", "orders.decide", "orders.misses.view", "orders.view", "payments.cash.confirm", "payments.online.review", "payments.settings.manage", "printers.manage", "roles.manage", "staff.manage", "staff.read", "stations.manage", "tables.manage", "wifi.lookup", "wifi.manage" },
                TenantPermissions.All);
            Assert.Equal(new[] { TenantPermissions.BranchesRead, TenantPermissions.StaffManage, TenantPermissions.StaffRead },
                TenantPermissions.StaffUnion(fromRoles.Concat(personal)));
            Assert.Equal(fromRoles.Reverse(), TenantPermissions.StaffUnion(fromRoles));
        }

        [Fact]
        public async Task OccupiedRoleCannotBeRetiredRegardlessOfMemberActivity()
        {
            var role = CreateRole();
            var commands = new RoleCommands(role) { RoleHasMembership = true };
            await using var context = CreateContext(commands);
            var audit = new RecordingAudit();
            var store = new TenantRoleStore(context, TimeProvider.System, audit);

            var result = await store.RetireRoleAsync(role.Id, Guid.NewGuid(), default);

            Assert.Equal(TenantRoleErrors.InUse(), result.Error);
            Assert.Empty(audit.Entries);
            Assert.NotNull(commands.MembershipQuery);
            Assert.DoesNotContain("tenant_users", commands.MembershipQuery, StringComparison.Ordinal);
            Assert.Null(context.TenantRoles.Local.Single().DeletedAt);
        }

        [Fact]
        public async Task RetirementKeepsTheRoleAndRecordsItsStatusChange()
        {
            var role = CreateRole();
            await using var context = CreateContext(new RoleCommands(role));
            var audit = new RecordingAudit();
            var store = new TenantRoleStore(context, TimeProvider.System, audit);
            var actor = Guid.NewGuid();

            Assert.True((await store.RetireRoleAsync(role.Id, actor, default)).IsSuccess);

            var tracked = Assert.Single(context.TenantRoles.Local);
            Assert.NotNull(tracked.DeletedAt);
            Assert.Equal(actor, tracked.DeletedBy);
            Assert.NotEqual(EntityState.Deleted, context.Entry(tracked).State);
            var entry = Assert.Single(audit.Entries);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!);
            Assert.Contains("Active", snapshot.Value.BeforeState);
            Assert.Contains("Retired", snapshot.Value.AfterState);
        }

        [Fact]
        public async Task PermissionsOnlyUpdateUsesAndReturnsTheDatabaseVersionAndAuditsTheGrantDiff()
        {
            var role = CreateRole();
            await using var context = CreateContext(new RoleCommands(role));
            var audit = new RecordingAudit();
            var store = new TenantRoleStore(context, TimeProvider.System, audit);
            var actor = Guid.NewGuid();

            var stale = await store.UpdateRoleAsync(role.Id, role.Name, [TenantPermissions.StaffRead], 6, actor, default);
            Assert.Equal(TenantRoleErrors.ConcurrencyConflict(), stale.Error);
            Assert.Empty(audit.Entries);

            var updated = await store.UpdateRoleAsync(role.Id, role.Name, [TenantPermissions.StaffRead], 7, actor, default);

            Assert.True(updated.IsSuccess);
            Assert.Equal("8", updated.Value.ConcurrencyToken);
            Assert.NotEqual(role.ConcurrencyStamp, Assert.Single(context.TenantRoles.Local).ConcurrencyStamp);
            var entry = Assert.Single(audit.Entries);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!);
            Assert.Equal("{\"PermissionCodes\":\"\"}", snapshot.Value.BeforeState);
            Assert.Equal("{\"PermissionCodes\":\"staff.read\"}", snapshot.Value.AfterState);
        }

        [Fact]
        public async Task OwnerRoleCannotBeRetiredAndStaffCannotExecuteAdministration()
        {
            var owner = TenantRole.Create(Guid.NewGuid(), "Owner", "owner", "test-stamp", Now);
            await using var context = CreateContext(new RoleCommands(owner));
            var audit = new RecordingAudit();
            var store = new TenantRoleStore(context, TimeProvider.System, audit);
            var staff = new FakeTask42Access { AccountAccess = new TenantAccountAccess(true, "staff", true) };
            var handler = new TenantRoleAdministrationHandler(store, new FakeTenantPermissionReader(), staff);

            Assert.Equal(AuthErrors.Forbidden(), (await store.RetireRoleAsync(owner.Id, Guid.NewGuid(), default)).Error);
            Assert.Equal(AuthErrors.Forbidden(), (await handler.Handle(new ListTenantRoles(), default)).Error);
            Assert.Empty(audit.Entries);
        }

        [Theory]
        [InlineData(TenantPermissions.RolesManage)]
        [InlineData(TenantPermissions.BranchesManage)]
        [InlineData(TenantPermissions.CafeProfileManage)]
        [InlineData("unknown")]
        public async Task OwnerCannotPersistNonStaffPermissionsInEitherGrantPath(string permission)
        {
            var owner = new FakeTask42Access();
            var handler = new TenantRoleAdministrationHandler(null!, new FakeTenantPermissionReader(), owner);

            var roleGrant = await handler.Handle(new CreateTenantRole("Manager", [permission]), default);
            var personalGrant = await handler.Handle(new ReplaceStaffPermissions(Guid.NewGuid(), [permission]), default);

            Assert.IsType<ValidationError>(roleGrant.Error);
            Assert.IsType<ValidationError>(personalGrant.Error);
        }

        [Fact]
        public async Task RoleCreationAndUpdateRejectNulBeforePersistence()
        {
            var handler = new TenantRoleAdministrationHandler(null!, new FakeTenantPermissionReader(), new FakeTask42Access());

            var create = await handler.Handle(new CreateTenantRole("Cafe\0Manager", []), default);
            var update = await handler.Handle(new UpdateTenantRole(Guid.NewGuid(), "Cafe\0Manager", [], "7"), default);

            Assert.IsType<ValidationError>(create.Error);
            Assert.IsType<ValidationError>(update.Error);
        }

        [Fact]
        public async Task RoleDetailSelectsClaimsAndVersionInOneCommand()
        {
            var roleId = Guid.NewGuid();
            var reads = 0;
            var stub = new TenantCommandStub((command, _) =>
            {
                reads++;
                Assert.Contains("tenant_roles", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("tenant_role_claims", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("xmin", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("deleted_at IS NULL", command.CommandText, StringComparison.Ordinal);
                Assert.Contains(command.Parameters.Cast<DbParameter>(), parameter => Equals(roleId, parameter.Value));
                return TenantCommandStub.Scalars<Guid>();
            });
            await using var context = stub.CreateContext();
            var store = new TenantRoleStore(context, TimeProvider.System, new RecordingAudit());

            Assert.Null(await store.GetRoleAsync(roleId, default));
            Assert.Equal(1, reads);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PersonalPermissionReplacementRemovesOldGrantsAndAuditsTheNewSet(bool clear)
        {
            var staffId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var existing = TenantUserClaim.Create(Guid.NewGuid(), staffId, TenantPermissions.ClaimType, TenantPermissions.StaffRead, Now);
            var stub = new TenantCommandStub((command, eventData) =>
            {
                if (command.CommandText.Contains("tenant_user_claims", StringComparison.Ordinal))
                {
                    Assert.Contains(TenantPermissions.ClaimType, command.CommandText, StringComparison.Ordinal);
                    return TenantCommandStub.EntityRow(command, eventData, existing);
                }

                Assert.Contains("tenant_users", command.CommandText, StringComparison.Ordinal);
                return TenantCommandStub.Scalars(true);
            }, AssertUserLock);
            await using var context = stub.CreateContext();
            var audit = new RecordingAudit();
            var store = new TenantRoleStore(context, TimeProvider.System, audit);

            var result = await store.ReplacePersonalPermissionsAsync(staffId, clear ? [] : [TenantPermissions.StaffManage], actorId, default);

            Assert.True(result.IsSuccess);
            var changes = context.ChangeTracker.Entries<TenantUserClaim>().ToArray();
            Assert.Equal(EntityState.Deleted, Assert.Single(changes, entry => entry.Entity.Id == existing.Id).State);
            if (clear)
            {
                Assert.Single(changes);
            }
            else
            {
                var added = Assert.Single(changes, entry => entry.State == EntityState.Added).Entity;
                Assert.Equal(staffId, added.TenantUserId);
                Assert.Equal(TenantPermissions.StaffManage, added.ClaimValue);
            }

            var entry = Assert.Single(audit.Entries);
            Assert.Equal(actorId, entry.ActorUserId);
            var snapshots = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains(TenantPermissions.StaffRead, snapshots.BeforeState);
            Assert.Equal(clear ? "{\"PersonalPermissionCodes\":\"\"}" : "{\"PersonalPermissionCodes\":\"staff.manage\"}", snapshots.AfterState);
        }

        [Fact]
        public async Task MembershipReplacementTracksAndAuditsRemovalAndAssignment()
        {
            var staffId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var old = TenantUserRole.Create(Guid.NewGuid(), staffId, Guid.NewGuid(), Now);
            var role = CreateRole();
            var stub = new TenantCommandStub((command, eventData) =>
            {
                if (command.CommandText.Contains("tenant_user_roles", StringComparison.Ordinal))
                {
                    return TenantCommandStub.EntityRow(command, eventData, old);
                }

                if (command.CommandText.Contains("tenant_roles", StringComparison.Ordinal))
                {
                    return TenantCommandStub.EntityRow(command, eventData, role);
                }

                Assert.Contains("tenant_users", command.CommandText, StringComparison.Ordinal);
                return TenantCommandStub.Scalars(true);
            }, command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext();
            var audit = new RecordingAudit();
            var store = new TenantRoleStore(context, TimeProvider.System, audit);

            Assert.True((await store.ReplaceMembershipsAsync(staffId, [role.Id], actorId, default)).IsSuccess);

            var links = context.ChangeTracker.Entries<TenantUserRole>().ToArray();
            Assert.Equal(old.RoleId, Assert.Single(links, link => link.State == EntityState.Deleted).Entity.RoleId);
            Assert.Equal(role.Id, Assert.Single(links, link => link.State == EntityState.Added).Entity.RoleId);
            Assert.Equal(new[] { "staff.roles.removed", "staff.roles.assigned" }, audit.Entries.Select(entry => entry.Action));
            Assert.All(audit.Entries, entry => Assert.Equal(staffId, entry.TargetId));
        }

        private static void AssertUserLock(DbCommand command)
        {
            Assert.Contains("FROM tenant_users", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal);
        }

        private static TenantRole CreateRole() =>
            TenantRole.Create(Guid.NewGuid(), "Manager", "manager", "test-stamp", Now);

        private static TenantDbContext CreateContext(RoleCommands commands) =>
            new(new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=unit-test;Timeout=1")
                .AddInterceptors(commands, new SuppressConnectionOpen(), new ReturnUpdatedVersion())
                .Options);

        private sealed class RoleCommands(TenantRole role) : DbCommandInterceptor
        {
            public bool RoleHasMembership { get; init; }
            public string? MembershipQuery { get; private set; }

            public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Assert.Contains("FROM tenant_roles", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal);
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                var rows = new DataTable();
                if (command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal))
                {
                    var isMembershipQuery = command.CommandText.Contains("tenant_user_roles", StringComparison.Ordinal);
                    if (isMembershipQuery) MembershipQuery = command.CommandText;
                    rows.Columns.Add("exists", typeof(bool));
                    rows.Rows.Add(isMembershipQuery && RoleHasMembership);
                }
                else if (command.CommandText.Contains("FROM tenant_roles", StringComparison.Ordinal))
                {
                    var fields = eventData.Context!.Model.FindEntityType(typeof(TenantRole))!.GetProperties()
                        .ToDictionary(property => property.GetColumnName());
                    var values = new List<object?>();
                    foreach (var field in command.CommandText.Split('\n')[0]["SELECT ".Length..].Split(", "))
                    {
                        var name = field[(field.IndexOf('.') + 1)..].Trim().Trim('"');
                        var property = fields[name];
                        rows.Columns.Add(name, Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
                        values.Add(name == "xmin" ? 7U : property.PropertyInfo?.GetValue(role));
                    }

                    rows.Rows.Add(values.ToArray());
                }

                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(rows.CreateDataReader()));
            }
        }

        private sealed class SuppressConnectionOpen : DbConnectionInterceptor
        {
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
                DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());
        }

        private sealed class ReturnUpdatedVersion : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                var tracked = Assert.Single(eventData.Context!.ChangeTracker.Entries<TenantRole>());
                Assert.True(tracked.Property(role => role.ConcurrencyStamp).IsModified);
                tracked.Property<uint>("xmin").CurrentValue = 8;
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }

        private sealed class RecordingAudit : ITenantAuditWriter
        {
            public List<TenantAuditEntry> Entries { get; } = [];

            public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
            {
                Entries.Add(entry);
                return Task.FromResult(Result.Success());
            }
        }
    }
}
