using System.Text.Json;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.Queries;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.UnitTests.Orders
{
    public sealed class GuestOrderStatusQueryTests
    {
        private const string Reference = "Abcdefghijklmnopqrstuv";
        private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly DateTimeOffset SubmittedAt = DateTimeOffset.Parse("2026-11-22T10:00:00Z");
        private static readonly DateTimeOffset Deadline = SubmittedAt.AddMinutes(5);

        [Fact]
        public async Task Status_ReturnsTheGuestVisibleFieldsAndNeverStaffIdentityOrDeviceData()
        {
            var store = new FakeGuestOrderStore(Entry(OrderStatus.Accepted));
            var handler = new GetGuestOrderStatusQueryHandler(new FakeSender(store), store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery(Reference), default);

            Assert.True(result.IsSuccess);
            var status = result.Value!;
            Assert.Equal(7, status.OrderNumber);
            Assert.Equal("Accepted", status.Status);
            Assert.Null(status.RejectionReason);
            Assert.Equal(4, status.TableNumber);
            Assert.Equal("DineIn", status.ServiceType);
            Assert.Equal(SubmittedAt, status.SubmittedAt);
            Assert.Equal(Deadline, status.ResponseDeadline);
            Assert.Equal(42.50m, status.TotalAmount);
            var line = Assert.Single(status.Items);
            Assert.Equal("Latte", line.ProductName);
            Assert.Equal("Regular", line.SizeName);
            Assert.Equal(2, line.Quantity);
            Assert.Equal(new[] { "Milk" }, line.AddonNames);

            var json = JsonSerializer.Serialize(status);
            Assert.DoesNotContain("\"Id\"", json);
            Assert.DoesNotContain("respondedBy", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("guestDevice", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stationName", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("guestDisplayName", json, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(OrderStatus.Accepted, null)]
        [InlineData(OrderStatus.Rejected, "Out of croissants")]
        [InlineData(OrderStatus.Expired, null)]
        public async Task Status_ShowsTheRejectionReasonOnlyForRejectedOrders(OrderStatus status, string? expected)
        {
            var store = new FakeGuestOrderStore(Entry(status));
            var handler = new GetGuestOrderStatusQueryHandler(new FakeSender(store), store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery(Reference), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(status.ToString(), result.Value!.Status);
            Assert.Equal(expected, result.Value.RejectionReason);
        }

        [Fact]
        public async Task Status_APendingOrderInsideItsWindowStaysPending()
        {
            var store = new FakeGuestOrderStore(Entry(OrderStatus.Pending));
            var sender = new FakeSender(store) { ExpiresStaleOrders = false };
            var handler = new GetGuestOrderStatusQueryHandler(sender, store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery(Reference), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(nameof(OrderStatus.Pending), result.Value!.Status);
            Assert.Null(result.Value.RejectionReason);
            Assert.IsType<ExpireStaleOrdersCommand>(Assert.Single(sender.Sent));
        }

        [Fact]
        public async Task Status_APendingOrderPastItsDeadlineIsReturnedAsExpiredOnRead()
        {
            var store = new FakeGuestOrderStore(Entry(OrderStatus.Pending));
            var sender = new FakeSender(store);
            var handler = new GetGuestOrderStatusQueryHandler(sender, store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery(Reference), default);

            Assert.True(result.IsSuccess);
            Assert.Equal("Expired", result.Value!.Status);
            var command = Assert.IsType<ExpireStaleOrdersCommand>(Assert.Single(sender.Sent));
            Assert.Equal(OrderId, command.OrderId);
            Assert.Null(command.BranchId);
            Assert.Equal(2, store.ReadCount);
            Assert.Equal(Reference, store.LastReference);
        }

        [Fact]
        public async Task Status_AnAlreadyDecidedOrderSkipsTheExpiryWrite()
        {
            var store = new FakeGuestOrderStore(Entry(OrderStatus.Rejected));
            var sender = new FakeSender(store);
            var handler = new GetGuestOrderStatusQueryHandler(sender, store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery(Reference), default);

            Assert.True(result.IsSuccess);
            Assert.Empty(sender.Sent);
            Assert.Equal(1, store.ReadCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short")]
        [InlineData("Abcdefghijklmnopqrstuvw")]
        [InlineData("Abcdefghijklmnopqrstu+")]
        [InlineData("Abcdefghijklmnopqrstu ")]
        public async Task Status_AReferenceOutsideTheBase64UrlShapeNeverReachesTheStore(string? reference)
        {
            var store = new FakeGuestOrderStore(Entry(OrderStatus.Pending));
            var handler = new GetGuestOrderStatusQueryHandler(new FakeSender(store), store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery(reference), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, store.ReadCount);
        }

        [Fact]
        public async Task Status_UnknownAndMalformedReferencesReturnTheIdenticalNotFound()
        {
            var unknownStore = new FakeGuestOrderStore(null);
            var unknown = await new GetGuestOrderStatusQueryHandler(new FakeSender(unknownStore), unknownStore)
                .Handle(new GetGuestOrderStatusQuery("Zbcdefghijklmnopqrstuv"), default);
            var malformedStore = new FakeGuestOrderStore(Entry(OrderStatus.Pending));
            var malformed = await new GetGuestOrderStatusQueryHandler(new FakeSender(malformedStore), malformedStore)
                .Handle(new GetGuestOrderStatusQuery("nope"), default);

            Assert.False(unknown.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, unknown.Error!.Code);
            Assert.Equal(unknown.Error.Code, malformed.Error!.Code);
            Assert.Equal(unknown.Error.Type, malformed.Error.Type);
            Assert.Equal(unknown.Error.Message, malformed.Error.Message);
        }

        [Fact]
        public async Task Status_TheStoreIsReadByTheTrimmedPublicReference()
        {
            var store = new FakeGuestOrderStore(Entry(OrderStatus.Accepted));
            var handler = new GetGuestOrderStatusQueryHandler(new FakeSender(store), store);

            var result = await handler.Handle(new GetGuestOrderStatusQuery($"  {Reference}  "), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Reference, store.LastReference);
        }

        private static GuestOrderEntry Entry(OrderStatus status) => new(
            OrderId,
            7,
            4,
            "DineIn",
            status.ToString(),
            "Out of croissants",
            SubmittedAt,
            Deadline,
            42.50m,
            [new GuestOrderEntryItem("Latte", "Regular", 2, ["Milk"])]);

        private sealed class FakeGuestOrderStore(GuestOrderEntry? entry) : IGuestOrderStore
        {
            public GuestOrderEntry? Entry { get; private set; } = entry;

            public int ReadCount { get; private set; }

            public string? LastReference { get; private set; }

            public void Expire() => Entry = Entry! with { Status = nameof(OrderStatus.Expired) };

            public Task<GuestOrderEntry?> FindByPublicReferenceAsync(
                string publicReference,
                CancellationToken cancellationToken)
            {
                ReadCount++;
                LastReference = publicReference;
                return Task.FromResult(Entry);
            }
        }

        private sealed class FakeSender(FakeGuestOrderStore store) : ISender
        {
            public List<object> Sent { get; } = [];

            public bool ExpiresStaleOrders { get; init; } = true;

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Sent.Add(request);
                if (request is ExpireStaleOrdersCommand)
                {
                    if (ExpiresStaleOrders)
                    {
                        store.Expire();
                    }

                    return Task.FromResult((TResponse)(object)Result.Success<IReadOnlyList<Guid>>([OrderId]));
                }

                throw new NotSupportedException("The guest status handler sends only the expiry command.");
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest => throw new NotSupportedException();

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
