using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;

namespace CafeHub.UnitTests.Orders
{
    public sealed class ExpireStaleOrdersCommandHandlerTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OrderId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-22T10:00:00Z");

        [Fact]
        public async Task TheBranchScopeIsForwardedToTheStore()
        {
            var store = new FakeOrderExpiryStore([OrderId]);
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ExpireStaleOrdersCommand(BranchId, null), default);

            Assert.True(result.IsSuccess);
            Assert.Equal([OrderId], result.Value);
            Assert.Equal(BranchId, store.LastBranchId);
            Assert.Null(store.LastOrderId);
            Assert.Equal(Now, store.LastNow);
        }

        [Fact]
        public async Task TheSingleOrderScopeIsForwardedToTheStore()
        {
            var store = new FakeOrderExpiryStore([]);
            var handler = CreateHandler(store);

            await handler.Handle(new ExpireStaleOrdersCommand(null, OrderId), default);

            Assert.Null(store.LastBranchId);
            Assert.Equal(OrderId, store.LastOrderId);
        }

        [Fact]
        public async Task AnEmptyExpiryIsASuccessfulNoOp()
        {
            var store = new FakeOrderExpiryStore([]);
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ExpireStaleOrdersCommand(BranchId, null), default);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value);
        }

        private static ExpireStaleOrdersCommandHandler CreateHandler(FakeOrderExpiryStore store) =>
            new(store, new FixedClock(Now));

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeOrderExpiryStore(IReadOnlyList<Guid> expiredIds) : IOrderExpiryStore
        {
            public Guid? LastBranchId { get; private set; }

            public Guid? LastOrderId { get; private set; }

            public DateTimeOffset LastNow { get; private set; }

            public Task<IReadOnlyList<Guid>> ExpireStaleOrdersAsync(
                Guid? branchId,
                Guid? orderId,
                DateTimeOffset now,
                CancellationToken cancellationToken)
            {
                LastBranchId = branchId;
                LastOrderId = orderId;
                LastNow = now;
                return Task.FromResult(expiredIds);
            }
        }
    }
}
