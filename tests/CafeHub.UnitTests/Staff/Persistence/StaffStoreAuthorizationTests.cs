using System.Data.Common;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Staff;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Staff.Persistence
{
    public sealed class StaffStoreAuthorizationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreateRejectsNonOwnersBeforeReadingTheBranchOrCheckingTheEmail(bool signedIn)
        {
            var branchId = Guid.NewGuid();
            var access = new FakeTask42Access { AccountAccess = signedIn ? new(true, "staff", true) { BranchId = branchId } : null };
            var actorId = Guid.NewGuid();
            var stub = new TenantCommandStub((_, _) => throw new InvalidOperationException("Authorization must precede database access."));
            await using var context = stub.CreateContext();
            var audit = new FakeStaffAuditWriter();
            var store = new StaffStore(context, new PasswordHasher<TenantUser>(), audit, TimeProvider.System, access);

            var result = await store.CreateAsync("Staff", "staff@example.test", null, branchId, "unused", actorId, default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task OwnerStillReceivesTheTenantWideDuplicateEmailConflict()
        {
            var branch = Branch.Create(Guid.NewGuid(), "Branch", BranchLifecycleStatus.Active, null, null, null, DateTimeOffset.UtcNow);
            var checkedEmail = false;
            var stub = new TenantCommandStub((command, eventData) =>
            {
                if (command.CommandText.Contains("FROM branches", StringComparison.Ordinal))
                {
                    return TenantCommandStub.EntityRow(command, eventData, branch);
                }

                Assert.Contains("normalized_email", command.CommandText, StringComparison.Ordinal);
                Assert.DoesNotContain("branch_id", command.CommandText, StringComparison.Ordinal);
                checkedEmail = true;
                return TenantCommandStub.Scalars(true);
            }, command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            await using var context = stub.CreateContext();
            var audit = new FakeStaffAuditWriter();
            var store = new StaffStore(context, new PasswordHasher<TenantUser>(), audit, TimeProvider.System, new FakeTask42Access());

            var result = await store.CreateAsync("Staff", "Staff@example.test", null, branch.Id, "unused", Guid.NewGuid(), default);

            Assert.True(checkedEmail);
            Assert.Equal(StaffErrors.EmailConflict(), result.Error);
            Assert.Empty(audit.Entries);
        }

        [Theory]
        [InlineData("update", true)]
        [InlineData("update", false)]
        [InlineData("deactivate", true)]
        [InlineData("deactivate", false)]
        [InlineData("reactivate", true)]
        [InlineData("reactivate", false)]
        public async Task DelegatedWritesRequireTheTargetToBeInTheActorsActiveBranch(string operation, bool sameBranch)
        {
            var actorId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var branch = Branch.Create(Guid.NewGuid(), "Branch", BranchLifecycleStatus.Active, null, null, null, now);
            var staff = TenantUser.CreateStaff(Guid.NewGuid(), "staff", "staff", "Staff", "staff@example.test",
                "staff@example.test", null, "test-hash", "test-stamp", "test-concurrency-stamp", sameBranch ? branch.Id : Guid.NewGuid(), now);
            if (operation == "reactivate")
            {
                staff.Deactivate(now, actorId);
            }

            var stub = new TenantCommandStub((command, eventData) =>
            {
                var sql = command.CommandText;
                if (sql.Contains("FOR UPDATE", StringComparison.Ordinal))
                {
                    Assert.Contains("branch_id =", sql, StringComparison.Ordinal);
                    Assert.Contains("user_kind = 'Staff'", sql, StringComparison.Ordinal);
                    var ids = command.Parameters.Cast<DbParameter>().Select(parameter => Assert.IsType<Guid>(parameter.Value)).ToArray();
                    Assert.Contains(branch.Id, ids);
                    var lockedId = Assert.Single(ids, id => id != branch.Id);
                    return !sameBranch && lockedId == staff.Id ? TenantCommandStub.Scalars<Guid>() : TenantCommandStub.Scalars(lockedId);
                }

                Assert.True(sameBranch, "Out-of-scope writes must stop after the scoped row lock.");
                if (sql.StartsWith("SELECT EXISTS", StringComparison.Ordinal))
                {
                    Assert.Contains("lifecycle_status = 'Active'", sql, StringComparison.Ordinal);
                    return TenantCommandStub.Scalars(true);
                }

                if (sql.Contains("FROM refresh_tokens", StringComparison.Ordinal))
                {
                    return TenantCommandStub.Scalars<Guid>();
                }

                if (sql.Contains("FROM branches", StringComparison.Ordinal))
                {
                    return TenantCommandStub.EntityRow(command, eventData, branch);
                }

                Assert.Contains("branch_id", sql, StringComparison.Ordinal);
                return TenantCommandStub.EntityRow(command, eventData, staff);
            }, command => Assert.Contains("FOR UPDATE", command.CommandText, StringComparison.Ordinal));
            var saves = new SuppressSave();
            await using var context = stub.CreateContext(saves);
            var audit = new FakeStaffAuditWriter();
            var access = new FakeTask42Access { AccountAccess = new(true, "staff", true) { BranchId = branch.Id } };
            var store = new StaffStore(context, new PasswordHasher<TenantUser>(), audit, TimeProvider.System, access);

            var result = operation switch
            {
                "update" => await store.UpdateAsync(staff.Id, "New name", null, branch.Id, "1", actorId, default),
                "deactivate" => await store.DeactivateAsync(staff.Id, actorId, default),
                "reactivate" => await store.ReactivateAsync(staff.Id, actorId, default),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };

            Assert.Equal(sameBranch, result.IsSuccess);
            Assert.Equal(sameBranch ? 1 : 0, saves.Count);
            if (sameBranch)
            {
                Assert.Equal(staff.Id, Assert.Single(audit.Entries).TargetId);
                var updated = Assert.Single(context.TenantUsers.Local);
                Assert.Equal(operation == "update" ? "New name" : "Staff", updated.FullName);
                Assert.Equal(operation == "deactivate", updated.DeletedAt.HasValue);
            }
            else
            {
                Assert.Equal(StaffErrors.NotFound(), result.Error);
                Assert.Empty(audit.Entries);
            }
        }

        [Fact]
        public async Task DelegatedProfileUpdateCannotTransferAStaffMember()
        {
            var access = new FakeTask42Access { AccountAccess = new(true, "staff", true) { BranchId = Guid.NewGuid() } };
            var stub = new TenantCommandStub((_, _) => throw new InvalidOperationException("A staff transfer must be rejected before database access."));
            await using var context = stub.CreateContext();
            var store = new StaffStore(context, new PasswordHasher<TenantUser>(), new FakeStaffAuditWriter(), TimeProvider.System, access);

            var result = await store.UpdateAsync(Guid.NewGuid(), "New name", null, Guid.NewGuid(), "1", Guid.NewGuid(), default);

            Assert.Equal(StaffErrors.NotFound(), result.Error);
        }

        private sealed class SuppressSave : SaveChangesInterceptor
        {
            public int Count { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }
    }
}
