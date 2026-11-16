using System.Linq;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests;

namespace CafeHub.UnitTests.Tables.Queries
{
    public sealed class ListTablesQueryHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeTableStore store = new();
        private readonly FakeTask42Access access = new() { TenantId = TenantId, UserId = ActorId };

        public ListTablesQueryHandlerTests()
        {
            store.Items = new[]
            {
                TableResponse(1, BranchTableStatus.Active),
                TableResponse(2, BranchTableStatus.Active),
                TableResponse(3, BranchTableStatus.Inactive),
            };
        }

        [Fact]
        public async Task List_ReturnsTheBranchPageOrderedByNumber()
        {
            var handler = new ListTablesQueryHandler(store, access, new PaginationOptions());

            var result = await handler.Handle(
                new ListTablesQuery
                {
                    BranchId = BranchId,
                    Page = 1,
                    PageSize = 20,
                    IncludeTotalCount = true,
                },
                default);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.Items.Count);
            Assert.Equal(1, result.Value.Items[0].Number);
            Assert.Equal(2, result.Value.Items[1].Number);
            Assert.Equal(3, result.Value.Items[2].Number);
            Assert.Equal(3, result.Value.TotalCount);
            Assert.Equal(BranchId, Assert.Single(store.ListRequests).BranchId);
        }

        [Fact]
        public async Task List_ForAnotherBranchReturnsUniformNotFoundWithoutCallingTheStore()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = Guid.NewGuid() };
            var handler = new ListTablesQueryHandler(store, access, new PaginationOptions());

            var result = await handler.Handle(
                new ListTablesQuery { BranchId = BranchId, Page = 1, PageSize = 20, IncludeTotalCount = false },
                default);

            Assert.Equal(TablesErrors.NotFound(), result.Error);
            Assert.Empty(store.ListRequests);
        }

        [Fact]
        public async Task List_ForTheStaffMembersOwnBranchIsAllowed()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new ListTablesQueryHandler(store, access, new PaginationOptions());

            var result = await handler.Handle(
                new ListTablesQuery { BranchId = BranchId, Page = 1, PageSize = 20, IncludeTotalCount = false },
                default);

            Assert.True(result.IsSuccess);
            Assert.Single(store.ListRequests);
        }

        [Fact]
        public async Task List_FiltersByStatusAndNumberRange()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new ListTablesQueryHandler(store, access, new PaginationOptions());

            var active = await handler.Handle(
                new ListTablesQuery { BranchId = BranchId, Page = 1, PageSize = 20, IncludeTotalCount = false, Status = BranchTableStatus.Active },
                default);
            Assert.True(active.IsSuccess);
            Assert.Equal(2, active.Value.Items.Count);

            var range = await handler.Handle(
                new ListTablesQuery { BranchId = BranchId, Page = 1, PageSize = 20, IncludeTotalCount = false, FromNumber = 2, ToNumber = 2 },
                default);
            Assert.True(range.IsSuccess);
            Assert.Single(range.Value.Items);
            Assert.Equal(2, range.Value.Items[0].Number);
        }

        [Fact]
        public async Task List_PagesCorrectly()
        {
            access.AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId };
            var handler = new ListTablesQueryHandler(store, access, new PaginationOptions());

            var page = await handler.Handle(
                new ListTablesQuery { BranchId = BranchId, Page = 1, PageSize = 2, IncludeTotalCount = true },
                default);

            Assert.True(page.IsSuccess);
            Assert.Equal(2, page.Value.Items.Count);
            Assert.True(page.Value.HasNextPage);
            Assert.Equal(3, page.Value.TotalCount);
        }

        private static TableResponse TableResponse(int number, BranchTableStatus status) =>
            new()
            {
                Id = Guid.NewGuid(),
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
            public List<TableListRequest> ListRequests { get; } = new();
            public IReadOnlyList<TableResponse> Items { get; set; } = [];
            public TableResponse? GetByIdResponse { get; set; }
            public Error? Failure { get; set; }

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
                CancellationToken cancellationToken) =>
            Task.FromResult(GetByIdResponse);

            public Task<TableListPage> ListAsync(TableListRequest request, CancellationToken cancellationToken)
            {
                ListRequests.Add(request);
                var query = Items.AsQueryable();
                if (request.Status is { } status)
                {
                    query = query.Where(table => table.Status == status.ToString());
                }

                if (request.FromNumber is { } fromNumber)
                {
                    query = query.Where(table => table.Number >= fromNumber);
                }

                if (request.ToNumber is { } toNumber)
                {
                    query = query.Where(table => table.Number <= toNumber);
                }

                var totalCount = request.IncludeTotalCount ? query.Count() : (int?)null;
                var fetched = query
                    .OrderBy(table => table.Number)
                    .ThenBy(table => table.Id)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize + 1)
                    .ToList();

                return Task.FromResult(new TableListPage(fetched, totalCount));
            }

            public Task<int> CountActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
                Task.FromResult(0);

            public Task<int?> MaxActiveNumberAsync(Guid branchId, CancellationToken cancellationToken) =>
                Task.FromResult<int?>(null);
        }
    }
}
