using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Orders;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.UnitTests.Menu.Persistence;

namespace CafeHub.UnitTests.Orders.Persistence
{
    public sealed class OrderFeedStorePostgreSqlTests
    {
        [Task47PostgreSqlFact]
        public async Task TheFeedStoreProjectsTableNumberStationNamesAddonNamesAndAppliesTheStationPreFilter()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = database.CreateContext();
            var applicator = new NpgsqlTenantMigrationApplicator(new() { AdminConnectionString = database.ConnectionString });
            await applicator.ApplyAsync(database.Name, default);

            var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
            var branch = Branch.Create(Guid.CreateVersion7(), "North", BranchLifecycleStatus.Active, null, null, null, now);
            var table = BranchTable.Create(Guid.CreateVersion7(), branch.Id, 42, null, "Abcdefghijklmnopqrstuv", now);
            var bar = Station.Create(Guid.CreateVersion7(), branch.Id, "Bar", 0, now);
            var kitchen = Station.Create(Guid.CreateVersion7(), branch.Id, "Kitchen", 1, now);
            var otherBranch = Branch.Create(Guid.CreateVersion7(), "South", BranchLifecycleStatus.Active, null, null, null, now);

            var first = CreateOrder(Guid.CreateVersion7(), branch.Id, table.Id, 1, now.AddMinutes(-10),
            [
                OrderItem.Create(Guid.CreateVersion7(), 1, Guid.CreateVersion7(), Guid.CreateVersion7(), bar.Id,
                    "Latte", "Regular", 15.00m, 2,
                    [OrderItemAddon.Create(Guid.CreateVersion7(), null, "Milk", 2m, now, null)], now, null),
                OrderItem.Create(Guid.CreateVersion7(), 2, Guid.CreateVersion7(), Guid.CreateVersion7(), null,
                    "Water", "Small", 5.00m, 1, [], now, null),
            ]);
            var second = CreateOrder(Guid.CreateVersion7(), branch.Id, table.Id, 2, now.AddMinutes(-5),
            [
                OrderItem.Create(Guid.CreateVersion7(), 1, Guid.CreateVersion7(), Guid.CreateVersion7(), kitchen.Id,
                    "Cake", "Slice", 8.00m, 1, [], now, null),
            ]);
            context.AddRange(branch, otherBranch, table, bar, kitchen, first, second);
            await context.SaveChangesAsync();

            var store = new OrderFeedStore(context);

            var feed = await store.ListFeedAsync(
                new OrderFeedListRequest(1, 10, true, branch.Id, null, null, null),
                default);

            Assert.Equal(2, feed.TotalCount);
            Assert.Equal(2, feed.FetchedItems.Count);
            Assert.Equal(2, feed.FetchedItems[0].OrderNumber);
            Assert.Equal("Kitchen", Assert.Single(feed.FetchedItems[0].Items).StationName);
            Assert.Equal(1, feed.FetchedItems[1].OrderNumber);
            Assert.Equal(42, feed.FetchedItems[1].TableNumber);
            Assert.Equal("Pending", feed.FetchedItems[1].Status);
            Assert.Equal("DineIn", feed.FetchedItems[1].ServiceType);
            Assert.Equal(now.AddMinutes(-10), feed.FetchedItems[1].SubmittedAt);
            var firstEntryItems = feed.FetchedItems[1].Items;
            Assert.Equal(2, firstEntryItems.Count);
            Assert.Equal("Bar", firstEntryItems[0].StationName);
            Assert.Equal(new[] { "Milk" }, firstEntryItems[0].AddonNames);
            Assert.Null(firstEntryItems[1].StationId);
            Assert.Null(firstEntryItems[1].StationName);

            var scoped = await store.ListFeedAsync(
                new OrderFeedListRequest(1, 10, false, branch.Id, OrderStatus.Pending, null, [bar.Id]),
                default);

            var scopedEntry = Assert.Single(scoped.FetchedItems);
            Assert.Equal(1, scopedEntry.OrderNumber);
            Assert.Equal(2, scopedEntry.Items.Count);

            var byId = await store.FindByIdAsync(branch.Id, first.Id, default);
            Assert.NotNull(byId);
            Assert.Equal(1, byId!.OrderNumber);
            Assert.Null(await store.FindByIdAsync(otherBranch.Id, first.Id, default));
        }

        private static Order CreateOrder(
            Guid id,
            Guid branchId,
            Guid tableId,
            int orderNumber,
            DateTimeOffset submittedAt,
            OrderItem[] items) => Order.Create(
                id,
                branchId,
                tableId,
                orderNumber,
                DateOnly.FromDateTime(submittedAt.UtcDateTime),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                "Sara",
                null,
                null,
                "Abcdefghijklmnopqrstu" + orderNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "idempotency-" + orderNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null,
                submittedAt,
                submittedAt.AddMinutes(5),
                items,
                null);
    }
}
