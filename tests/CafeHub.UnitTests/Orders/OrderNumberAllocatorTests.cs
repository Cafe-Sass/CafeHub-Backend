using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Infrastructure.Orders;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderNumberAllocatorTests
    {
        private static readonly Guid BranchId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid OtherBranchId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        private static readonly DateOnly Today = new(2026, 11, 22);

        private readonly FakeCounterStore store = new();
        private readonly SettableBusinessDate businessDate = new(Today);

        [Fact]
        public async Task FirstOrderOfTheDayGetsOneAndTheNextGetsTwo()
        {
            var allocator = CreateAllocator();

            Assert.Equal(1, (await allocator.AllocateAsync(BranchId, default)).Number);
            Assert.Equal(2, (await allocator.AllocateAsync(BranchId, default)).Number);
            Assert.Equal(3, (await allocator.AllocateAsync(BranchId, default)).Number);
        }

        [Fact]
        public async Task ANewBusinessDateRestartsAtOne()
        {
            var allocator = CreateAllocator();
            await allocator.AllocateAsync(BranchId, default);
            await allocator.AllocateAsync(BranchId, default);

            businessDate.Value = Today.AddDays(1);

            Assert.Equal(1, (await allocator.AllocateAsync(BranchId, default)).Number);
        }

        [Fact]
        public async Task TwoBranchesNeverShareCounters()
        {
            var allocator = CreateAllocator();
            await allocator.AllocateAsync(BranchId, default);

            Assert.Equal(1, (await allocator.AllocateAsync(OtherBranchId, default)).Number);
            Assert.Equal(2, (await allocator.AllocateAsync(BranchId, default)).Number);
        }

        [Fact]
        public async Task TheAllocatorUsesTheProvidersBusinessDate()
        {
            var otherDay = Today.AddDays(-3);
            businessDate.Value = otherDay;

            var allocation = await CreateAllocator().AllocateAsync(BranchId, default);

            Assert.Equal(otherDay, Assert.Single(store.Requests).BusinessDate);
            Assert.Equal(otherDay, allocation.BusinessDate);
        }

        [Fact]
        public async Task AnEmptyBranchIsRefused()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => CreateAllocator().AllocateAsync(Guid.Empty, default));
            Assert.Empty(store.Requests);
        }

        [Fact]
        public void TheAllocatorStatementIsTheSingleAtomicUpsert()
        {
            Assert.StartsWith("INSERT INTO order_number_counters", OrderNumberCounterStore.AllocateSql, StringComparison.Ordinal);
            Assert.Contains("ON CONFLICT (branch_id, business_date) DO UPDATE", OrderNumberCounterStore.AllocateSql, StringComparison.Ordinal);
            Assert.Contains(
                "SET last_number = order_number_counters.last_number + 1",
                OrderNumberCounterStore.AllocateSql,
                StringComparison.Ordinal);
            Assert.EndsWith("RETURNING last_number", OrderNumberCounterStore.AllocateSql, StringComparison.Ordinal);
        }

        private OrderNumberAllocator CreateAllocator() => new(store, businessDate);

        private sealed class SettableBusinessDate(DateOnly value) : ICafeBusinessDateProvider
        {
            public DateOnly Value { get; set; } = value;

            public Task<DateOnly> GetBusinessDateAsync(CancellationToken cancellationToken) =>
                Task.FromResult(Value);
        }

        private sealed class FakeCounterStore : IOrderNumberCounterStore
        {
            private readonly Dictionary<(Guid BranchId, DateOnly BusinessDate), int> counters = [];

            public List<(Guid BranchId, DateOnly BusinessDate)> Requests { get; } = [];

            public Task<int> IncrementAsync(
                Guid branchId, DateOnly businessDate, CancellationToken cancellationToken)
            {
                Requests.Add((branchId, businessDate));
                var key = (branchId, businessDate);
                counters[key] = counters.GetValueOrDefault(key) + 1;
                return Task.FromResult(counters[key]);
            }
        }
    }
}
