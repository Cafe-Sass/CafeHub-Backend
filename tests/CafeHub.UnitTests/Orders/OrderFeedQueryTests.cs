using System.Text.Json;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Orders.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderFeedQueryTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StationOne = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid StationTwo = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-22T10:00:00Z");

        [Fact]
        public async Task Feed_StaffWithoutABranchIdUsesTheirOwnBranch()
        {
            var store = new FakeOrderFeedStore(request => new OrderFeedPage([], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope([StationOne]));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, store.LastListRequest!.BranchId);
            Assert.Null(store.LastListRequest.Status);
        }

        [Fact]
        public async Task Feed_StaffAreFixedToTheirBranchAndAForeignBranchReturnsTheUniformNotFound()
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery { BranchId = OtherBranchId }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Null(store.LastListRequest);
        }

        [Fact]
        public async Task Feed_AnOwnerMustPassABranchId()
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null));
            var handler = CreateFeedHandler(store, OwnerUser(), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.BranchRequiredCode, result.Error!.Code);
            Assert.Null(store.LastListRequest);
        }

        [Fact]
        public async Task Feed_AnOwnerReadsThePassedBranchWithEveryStationVisible()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"), Item(StationTwo, "Kitchen"), Item(null, null));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([entry], 1));
            var handler = CreateFeedHandler(store, OwnerUser(), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery { BranchId = BranchId }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, store.LastListRequest!.BranchId);
            Assert.Null(store.LastListRequest.StationIds);
            var item = Assert.Single(result.Value!.Items);
            Assert.Equal(3, item.Items.Count);
        }

        [Fact]
        public async Task Feed_StationScopedStaffSeeOnlyTheirStationsItemsPlusUnroutedItems()
        {
            var visible = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"), Item(StationTwo, "Kitchen"), Item(null, null));
            var dropped = Entry(Guid.CreateVersion7(), 2, Now, Item(StationTwo, "Kitchen"));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([visible, dropped], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope([StationOne]));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.True(result.IsSuccess);
            var item = Assert.Single(result.Value!.Items);
            Assert.Equal(2, item.Items.Count);
            Assert.Equal("Bar", item.Items[0].StationName);
            Assert.Null(item.Items[1].StationName);
        }

        [Fact]
        public async Task Feed_MultipleAssignedStationsKeepAllTheirItems()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"), Item(StationTwo, "Kitchen"), Item(Guid.CreateVersion7(), "Grill"), Item(null, null));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([entry], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope([StationOne, StationTwo]));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.True(result.IsSuccess);
            var stationNames = result.Value!.Items[0].Items.Select(line => line.StationName).ToArray();
            Assert.Equal(new string?[] { "Bar", "Kitchen", null }, stationNames);
        }

        [Fact]
        public async Task Feed_StaffWithoutStationAssignmentsSeeEveryStation()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"), Item(StationTwo, "Kitchen"));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([entry], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.True(result.IsSuccess);
            Assert.Null(store.LastListRequest!.StationIds);
            Assert.Equal(2, result.Value!.Items[0].Items.Count);
        }

        [Fact]
        public async Task Feed_TheUpdatedSinceCursorIsForwardedAndExclusiveAtTheBoundary()
        {
            var boundary = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"));
            var newer = Entry(Guid.CreateVersion7(), 2, Now.AddSeconds(1), Item(StationOne, "Bar"));
            var store = new FakeOrderFeedStore(request =>
                new OrderFeedPage(new[] { boundary, newer }.Where(entry => entry.UpdatedAt > request.UpdatedSince).ToArray(), null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery { UpdatedSince = Now }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Now, store.LastListRequest!.UpdatedSince);
            var item = Assert.Single(result.Value!.Items);
            Assert.Equal(2, item.OrderNumber);
        }

        [Theory]
        [InlineData("Pending", OrderStatus.Pending)]
        [InlineData("accepted", OrderStatus.Accepted)]
        [InlineData("Rejected", OrderStatus.Rejected)]
        [InlineData("expired", OrderStatus.Expired)]
        public async Task Feed_TheStatusFilterAcceptsTheFourStatuses(string status, OrderStatus expected)
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery { Status = status }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, store.LastListRequest!.Status);
        }

        [Theory]
        [InlineData("Delivering")]
        [InlineData("1")]
        public async Task Feed_AnUnknownStatusIsRefused(string status)
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery { Status = status }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusInvalidCode, result.Error!.Code);
            Assert.Null(store.LastListRequest);
        }

        [Fact]
        public async Task Feed_PagingDerivesHasNextPageFromTheFetchedWindow()
        {
            var entries = new[]
            {
                Entry(Guid.CreateVersion7(), 1, Now.AddMinutes(-1), Now.AddMinutes(-6), Item(StationOne, "Bar")),
                Entry(Guid.CreateVersion7(), 2, Now.AddMinutes(-2), Now.AddMinutes(-7), Item(StationOne, "Bar")),
                Entry(Guid.CreateVersion7(), 3, Now.AddMinutes(-3), Now.AddMinutes(-8), Item(StationOne, "Bar")),
            };
            var store = new FakeOrderFeedStore(request => new OrderFeedPage(
                entries
                    .OrderByDescending(entry => entry.SubmittedAt)
                    .ThenByDescending(entry => entry.Id)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize + 1)
                    .ToArray(),
                null));
            var handler = CreateFeedHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var firstPage = await handler.Handle(new GetOrderFeedQuery { Page = 1, PageSize = 2 }, default);
            var secondPage = await handler.Handle(new GetOrderFeedQuery { Page = 2, PageSize = 2 }, default);

            Assert.True(firstPage.IsSuccess);
            Assert.True(firstPage.Value!.HasNextPage);
            Assert.Equal(2, firstPage.Value.Items.Count);
            Assert.Equal(1, firstPage.Value.Items[0].OrderNumber);
            Assert.True(secondPage.IsSuccess);
            Assert.False(secondPage.Value!.HasNextPage);
            Assert.Equal(3, secondPage.Value.Items[0].OrderNumber);
        }

        [Fact]
        public void Feed_ResponsesNeverCarryTheGuestDeviceId()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"));

            var json = JsonSerializer.Serialize(OrderFeedItem.From(entry));

            Assert.DoesNotContain("device", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Get_AForeignOrderIsIndistinguishableFromAMissingOne()
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null)) { Found = null };
            var handler = CreateGetHandler(store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderQuery(Guid.CreateVersion7()), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.LastBranchLookup);
        }

        [Fact]
        public async Task Get_StationScopedStaffWhoSeeNoItemsGetTheUniformNotFound()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(StationTwo, "Kitchen"));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null)) { Found = entry };
            var handler = CreateGetHandler(store, StaffUser(BranchId), new FakeStationScope([StationOne]));

            var result = await handler.Handle(new GetOrderQuery(entry.Id), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
        }

        [Fact]
        public async Task Get_StationScopedStaffSeeOnlyTheirStationItems()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(StationOne, "Bar"), Item(StationTwo, "Kitchen"), Item(null, null));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null)) { Found = entry };
            var handler = CreateGetHandler(store, StaffUser(BranchId), new FakeStationScope([StationOne]));

            var result = await handler.Handle(new GetOrderQuery(entry.Id), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.Items.Count);
        }

        [Fact]
        public async Task Get_TheOwnerLooksUpAcrossBranches()
        {
            var entry = Entry(Guid.CreateVersion7(), 1, Now, Item(null, null));
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null)) { Found = entry };
            var handler = CreateGetHandler(store, OwnerUser(), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderQuery(entry.Id), default);

            Assert.True(result.IsSuccess);
            Assert.Null(store.LastBranchLookup);
            Assert.Equal(1, result.Value!.OrderNumber);
        }

        [Fact]
        public async Task Feed_TheStaffBranchIsExpiredBeforeTheListIsBuilt()
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null));
            var sender = new FakeSender();
            var handler = CreateFeedHandler(sender, store, StaffUser(BranchId), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery(), default);

            Assert.True(result.IsSuccess);
            var expire = Assert.IsType<ExpireStaleOrdersCommand>(Assert.Single(sender.Sent));
            Assert.Equal(BranchId, expire.BranchId);
            Assert.Null(expire.OrderId);
        }

        [Fact]
        public async Task Feed_TheOwnerExpiresThePassedBranch()
        {
            var store = new FakeOrderFeedStore(_ => new OrderFeedPage([], null));
            var sender = new FakeSender();
            var handler = CreateFeedHandler(sender, store, OwnerUser(), new FakeStationScope(null));

            var result = await handler.Handle(new GetOrderFeedQuery { BranchId = BranchId }, default);

            Assert.True(result.IsSuccess);
            var expire = Assert.IsType<ExpireStaleOrdersCommand>(Assert.Single(sender.Sent));
            Assert.Equal(BranchId, expire.BranchId);
        }

        private static GetOrderFeedQueryHandler CreateFeedHandler(
            FakeOrderFeedStore store,
            FakeTenantUser user,
            FakeStationScope stations) =>
            new(new FakeSender(), store, user, stations, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        private static GetOrderFeedQueryHandler CreateFeedHandler(
            FakeSender sender,
            FakeOrderFeedStore store,
            FakeTenantUser user,
            FakeStationScope stations) =>
            new(sender, store, user, stations, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        private sealed class FakeSender : ISender
        {
            public List<object> Sent { get; } = [];

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Sent.Add(request);
                var response = request is ExpireStaleOrdersCommand
                    ? (TResponse)(object)Result.Success<IReadOnlyList<Guid>>(Array.Empty<Guid>())
                    : default;
                return Task.FromResult(response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest
            {
                Sent.Add(request);
                return Task.CompletedTask;
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            {
                Sent.Add(request);
                return Task.FromResult<object?>(null);
            }

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The feed handler must not create streams.");

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The feed handler must not create streams.");
        }

        private static GetOrderQueryHandler CreateGetHandler(
            FakeOrderFeedStore store,
            FakeTenantUser user,
            FakeStationScope stations) =>
            new(store, user, stations);

        private static FakeTenantUser StaffUser(Guid branchId) =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = branchId });

        private static FakeTenantUser OwnerUser() =>
            new(Guid.CreateVersion7(), new TenantAccountAccess(false, null, false));

        private static OrderFeedEntry Entry(Guid id, int number, DateTimeOffset updatedAt, params OrderFeedEntryItem[] items) =>
            Entry(id, number, updatedAt, null, items);

        private static OrderFeedEntry Entry(Guid id, int number, DateTimeOffset updatedAt, DateTimeOffset? submittedAt, params OrderFeedEntryItem[] items) =>
            new(id, number, 7, "DineIn", "Pending", "Sara", submittedAt ?? Now.AddMinutes(-5), Now.AddMinutes(5), updatedAt, 42.50m, items);

        private static OrderFeedEntryItem Item(Guid? stationId, string? stationName) =>
            new("Latte", "Regular", 2, stationId, stationName, ["Milk"]);

        private sealed class FakeOrderFeedStore(Func<OrderFeedListRequest, OrderFeedPage> listFeed) : IOrderFeedStore
        {
            public OrderFeedListRequest? LastListRequest { get; private set; }

            public Guid? LastBranchLookup { get; private set; }

            public OrderFeedEntry? Found { get; init; }

            public Task<OrderFeedPage> ListFeedAsync(OrderFeedListRequest request, CancellationToken cancellationToken)
            {
                LastListRequest = request;
                return Task.FromResult(listFeed(request));
            }

            public Task<OrderFeedEntry?> FindByIdAsync(Guid? branchId, Guid orderId, CancellationToken cancellationToken)
            {
                LastBranchLookup = branchId;
                return Task.FromResult(Found);
            }
        }

        private sealed class FakeStationScope(IReadOnlyList<Guid>? stationIds) : IStationScope
        {
            public Task<IReadOnlyList<Guid>?> GetVisibleStationIdsAsync(Guid userId, CancellationToken cancellationToken) =>
                Task.FromResult(stationIds);
        }

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => BranchId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }
    }
}
