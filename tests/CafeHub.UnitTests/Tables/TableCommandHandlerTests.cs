using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Commands;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TableCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeTableStore store = new();
        private readonly FakeTask42Access access = new() { TenantId = TenantId, UserId = ActorId };
        private readonly StubTenantContextAccessor tenantContext = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));

        public TableCommandHandlerTests()
        {
            store.AddResponse = TableResponses.Create(branchId: BranchId);
        }

        [Fact]
        public async Task Add_PassesTheBranchLabelAndActorToTheStore()
        {
            var handler = new AddTableCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new AddTableCommand(BranchId, "  Terrace 3  "), default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Added);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal("Terrace 3", call.Label);
            Assert.Equal(ActorId, call.ActorUserId);
        }

        [Fact]
        public async Task Add_ForAnotherBranchReturnsNotFoundWithoutCallingTheStore()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var handler = new AddTableCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new AddTableCommand(BranchId, null), default);

            Assert.Equal(TablesErrors.NotFound(), result.Error);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task Add_ForTheStaffMembersOwnBranchIsAllowed()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new AddTableCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new AddTableCommand(BranchId, null), default);

            Assert.True(result.IsSuccess);
            Assert.Single(store.Added);
        }

        [Fact]
        public async Task Add_WhenTheTenantContextDoesNotMatchReturnsUnauthorized()
        {
            var handler = new AddTableCommandHandler(store, access, tenantContext);
            access.TenantId = Guid.NewGuid();

            var result = await handler.Handle(new AddTableCommand(BranchId, null), default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task AddBulk_PassesTheBranchCountAndActorToTheStore()
        {
            var handler = new AddTablesBulkCommandHandler(store, access, tenantContext);
            store.BulkResponse = new TableBulkResult(5, 4, 8);

            var result = await handler.Handle(new AddTablesBulkCommand(BranchId, 5), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value.Count);
            Assert.Equal(4, result.Value.FromNumber);
            Assert.Equal(8, result.Value.ToNumber);
            var call = Assert.Single(store.BulkAdded);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal(5, call.Count);
            Assert.Equal(ActorId, call.ActorUserId);
        }

        [Fact]
        public async Task AddBulk_ForAnotherBranchReturnsNotFoundWithoutCallingTheStore()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var handler = new AddTablesBulkCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new AddTablesBulkCommand(BranchId, 5), default);

            Assert.Equal(TablesErrors.NotFound(), result.Error);
            Assert.Empty(store.BulkAdded);
        }

        [Fact]
        public async Task UpdateLabel_ForwardsTheStaffBranchScope()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var tableId = Guid.NewGuid();

            await new UpdateTableCommandHandler(store, access, tenantContext)
                .Handle(new UpdateTableCommand(tableId, "Counter 7", "7"), default);

            Assert.Equal(tableId, Assert.Single(store.Updated).TableId);
            Assert.Equal(BranchId, Assert.Single(store.Updated).ScopeBranchId);
            Assert.Equal("Counter 7", Assert.Single(store.Updated).Label);
            Assert.Equal("7", Assert.Single(store.Updated).ConcurrencyToken);
            Assert.Equal(ActorId, Assert.Single(store.Updated).ActorUserId);
        }

        [Fact]
        public async Task Deactivate_ForwardsTheStaffBranchScope()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var tableId = Guid.NewGuid();

            await new DeactivateTableCommandHandler(store, access, tenantContext)
                .Handle(new DeactivateTableCommand(tableId, "7"), default);

            Assert.Equal(tableId, Assert.Single(store.Deactivated).TableId);
            Assert.Equal(BranchId, Assert.Single(store.Deactivated).ScopeBranchId);
            Assert.Equal("7", Assert.Single(store.Deactivated).ConcurrencyToken);
            Assert.Equal(ActorId, Assert.Single(store.Deactivated).ActorUserId);
        }

        [Fact]
        public async Task Reactivate_ForwardsTheStaffBranchScope()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var tableId = Guid.NewGuid();

            await new ReactivateTableCommandHandler(store, access, tenantContext)
                .Handle(new ReactivateTableCommand(tableId, "7"), default);

            Assert.Equal(tableId, Assert.Single(store.Reactivated).TableId);
            Assert.Equal(BranchId, Assert.Single(store.Reactivated).ScopeBranchId);
            Assert.Equal("7", Assert.Single(store.Reactivated).ConcurrencyToken);
            Assert.Equal(ActorId, Assert.Single(store.Reactivated).ActorUserId);
        }

        [Fact]
        public async Task DeactivateHighest_PassesTheBranchCountAndActorToTheStore()
        {
            var handler = new DeactivateHighestTablesCommandHandler(store, access, tenantContext);
            store.DeactivateHighestResponse = new TableBulkResult(3, 7, 9);

            var result = await handler.Handle(new DeactivateHighestTablesCommand(BranchId, 3), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Count);
            Assert.Equal(7, result.Value.FromNumber);
            Assert.Equal(9, result.Value.ToNumber);
            var call = Assert.Single(store.DeactivatedHighest);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal(3, call.Count);
            Assert.Equal(ActorId, call.ActorUserId);
        }

        [Fact]
        public async Task DeactivateHighest_ForAnotherBranchReturnsNotFoundWithoutCallingTheStore()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var handler = new DeactivateHighestTablesCommandHandler(store, access, tenantContext);

            var result = await handler.Handle(new DeactivateHighestTablesCommand(BranchId, 3), default);

            Assert.Equal(TablesErrors.NotFound(), result.Error);
            Assert.Empty(store.DeactivatedHighest);
        }
    }

    internal sealed class FakeTableStore : ITableStore
    {
        public List<AddCall> Added { get; } = new();
        public List<BulkAddCall> BulkAdded { get; } = new();
        public List<UpdateCall> Updated { get; } = new();
        public List<StatusChangeCall> Deactivated { get; } = new();
        public List<StatusChangeCall> Reactivated { get; } = new();
        public List<DeactivateHighestCall> DeactivatedHighest { get; } = new();
        public TableResponse? AddResponse { get; set; }
        public TableBulkResult? BulkResponse { get; set; }
        public TableBulkResult? DeactivateHighestResponse { get; set; }
        public Error? Failure { get; set; }

        public Task<Result<TableResponse>> AddAsync(
            Guid branchId,
            string? label,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Added.Add(new AddCall(branchId, label, actorUserId));
            return Respond<TableResponse>(AddResponse!);
        }

        public Task<Result<TableBulkResult>> AddBulkAsync(
            Guid branchId,
            int count,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            BulkAdded.Add(new BulkAddCall(branchId, count, actorUserId));
            return Respond<TableBulkResult>(BulkResponse!);
        }

        public Task<Result<TableResponse>> UpdateLabelAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string? label,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Updated.Add(new UpdateCall(tableId, scopeBranchId, label, concurrencyToken, actorUserId));
            return Respond<TableResponse>(AddResponse!);
        }

        public Task<Result<TableResponse>> DeactivateAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Deactivated.Add(new StatusChangeCall(tableId, scopeBranchId, concurrencyToken, actorUserId));
            return Respond<TableResponse>(AddResponse!);
        }

        public Task<Result<TableResponse>> ReactivateAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Reactivated.Add(new StatusChangeCall(tableId, scopeBranchId, concurrencyToken, actorUserId));
            return Respond<TableResponse>(AddResponse!);
        }

        public Task<Result<TableBulkResult>> DeactivateHighestAsync(
            Guid branchId,
            int count,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            DeactivatedHighest.Add(new DeactivateHighestCall(branchId, count, actorUserId));
            return Respond<TableBulkResult>(DeactivateHighestResponse!);
        }

        public Task<TableResponse?> GetByIdAsync(
            Guid tableId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken) => Task.FromResult<TableResponse?>(null);

        public Task<int> CountActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<int?> MaxActiveNumberAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);

        private static Task<Result<T>> Respond<T>(T response) =>
            Task.FromResult(Result.Success(response));

        internal sealed record AddCall(Guid BranchId, string? Label, Guid ActorUserId);
        internal sealed record BulkAddCall(Guid BranchId, int Count, Guid ActorUserId);
        internal sealed record UpdateCall(
            Guid TableId,
            Guid? ScopeBranchId,
            string? Label,
            string ConcurrencyToken,
            Guid ActorUserId);
        internal sealed record StatusChangeCall(
            Guid TableId,
            Guid? ScopeBranchId,
            string ConcurrencyToken,
            Guid ActorUserId);
        internal sealed record DeactivateHighestCall(Guid BranchId, int Count, Guid ActorUserId);
    }

    internal static class TableResponses
    {
        public static TableResponse Create(Guid? id = null, Guid? branchId = null, BranchTableStatus status = BranchTableStatus.Active)
        {
            return new TableResponse
            {
                Id = id ?? Guid.NewGuid(),
                BranchId = branchId ?? Guid.NewGuid(),
                Number = 1,
                Label = null,
                Status = status.ToString(),
                QrCode = "Abcdefghijklmnopqrstuv",
                CreatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "7",
            };
        }
    }
}
