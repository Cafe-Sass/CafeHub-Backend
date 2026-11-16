using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests;

namespace CafeHub.UnitTests.Tables.Queries
{
    public sealed class GetTableQueryHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid TableId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        private readonly FakeTableStore store = new();
        private readonly FakeTask42Access access = new() { TenantId = TenantId, UserId = ActorId };

        public GetTableQueryHandlerTests()
        {
            store.GetByIdResponse = TableResponse(TableId, 3, BranchTableStatus.Active);
        }

        [Fact]
        public async Task Get_ReturnsTheTableInScope()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new GetTableQueryHandler(store, access);

            var result = await handler.Handle(new GetTableQuery(TableId), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(TableId, result.Value.Id);
            Assert.Equal(3, result.Value.Number);
            Assert.Equal(BranchTableStatus.Active.ToString(), result.Value.Status);
            Assert.Equal(BranchId, Assert.Single(store.GetByIdRequests).ScopeBranchId);
        }

        [Fact]
        public async Task Get_ForAnotherBranchReturnsUniformNotFound()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var handler = new GetTableQueryHandler(store, access);

            var result = await handler.Handle(new GetTableQuery(TableId), default);

            Assert.Equal(TablesErrors.NotFound(), result.Error);
            Assert.Single(store.GetByIdRequests);
        }

        [Fact]
        public async Task Get_ForTheStaffMembersOwnBranchIsAllowed()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new GetTableQueryHandler(store, access);

            var result = await handler.Handle(new GetTableQuery(TableId), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, Assert.Single(store.GetByIdRequests).ScopeBranchId);
        }

        [Fact]
        public async Task Get_ForAMissingTableReturnsUniformNotFound()
        {
            store.GetByIdResponse = null;
            var handler = new GetTableQueryHandler(store, access);

            var result = await handler.Handle(new GetTableQuery(TableId), default);

            Assert.Equal(TablesErrors.NotFound(), result.Error);
        }

        [Fact]
        public async Task Get_ByIdRespectsTheOwnerScopeNull()
        {
            access.AccountAccess = new(false, null, false);
            var handler = new GetTableQueryHandler(store, access);

            var result = await handler.Handle(new GetTableQuery(TableId), default);

            Assert.True(result.IsSuccess);
            Assert.Null(Assert.Single(store.GetByIdRequests).ScopeBranchId);
        }

        private static TableResponse TableResponse(Guid id, int number, BranchTableStatus status) =>
            new()
            {
                Id = id,
                BranchId = BranchId,
                Number = number,
                Label = null,
                Status = status.ToString(),
                QrCode = "Abcdefghijklmnopqrstuv",
                CreatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "7",
            };

        private sealed class FakeTableStore : ITableStore
        {
            public List<(Guid TableId, Guid? ScopeBranchId)> GetByIdRequests { get; } = new();
            public TableResponse? GetByIdResponse { get; set; }

            public Task<Result<TableResponse>> AddAsync(
                Guid branchId,
                string? label,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure<TableResponse>(Error.Failure("x", "x")));

            public Task<Result<TableBulkResult>> AddBulkAsync(
                Guid branchId,
                int count,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure<TableBulkResult>(Error.Failure("x", "x")));

            public Task<Result<TableResponse>> UpdateLabelAsync(
                Guid tableId,
                Guid? scopeBranchId,
                string? label,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure<TableResponse>(Error.Failure("x", "x")));

            public Task<Result<TableResponse>> DeactivateAsync(
                Guid tableId,
                Guid? scopeBranchId,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure<TableResponse>(Error.Failure("x", "x")));

            public Task<Result<TableResponse>> ReactivateAsync(
                Guid tableId,
                Guid? scopeBranchId,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure<TableResponse>(Error.Failure("x", "x")));

            public Task<Result<TableBulkResult>> DeactivateHighestAsync(
                Guid branchId,
                int count,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Failure<TableBulkResult>(Error.Failure("x", "x")));

            public Task<TableResponse?> GetByIdAsync(
                Guid tableId,
                Guid? scopeBranchId,
                CancellationToken cancellationToken)
            {
                GetByIdRequests.Add((tableId, scopeBranchId));
                if (GetByIdResponse is null)
                {
                    return Task.FromResult<TableResponse?>(null);
                }

                if (scopeBranchId is not null && GetByIdResponse.BranchId != scopeBranchId)
                {
                    return Task.FromResult<TableResponse?>(null);
                }

                return Task.FromResult<TableResponse?>(GetByIdResponse);
            }

            public Task<TableListPage> ListAsync(TableListRequest request, CancellationToken cancellationToken) =>
                Task.FromResult(new TableListPage([], null));

            public Task<int> CountActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
                Task.FromResult(0);

            public Task<int?> MaxActiveNumberAsync(Guid branchId, CancellationToken cancellationToken) =>
                Task.FromResult<int?>(null);
        }
    }
}
