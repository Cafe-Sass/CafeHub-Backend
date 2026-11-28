using System.Data;
using System.Data.Common;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Menu;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class GuestMenuStoreTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        [Fact]
        public async Task MenuQueryHidesInactiveProductsHiddenCategoriesAndBranchUnavailableRows()
        {
            var executed = Capture();
            await using var context = CreateContext(executed);
            var store = new GuestMenuStore(context);

            var page = await store.ListAsync(
                BranchId, null, null, null, null, null, 1, 20, DefaultSort(), false, default);

            Assert.Empty(page.Items);
            var command = Assert.Single(executed.Commands);
            var sql = command.Sql.Replace("\"", string.Empty);
            Assert.Contains("FROM products", sql);
            Assert.Contains("status = 'Active'", sql);
            Assert.Contains("menu_categories", sql);
            Assert.Contains("parent_id IS NULL", sql);
            Assert.Contains("EXISTS", sql);
            Assert.Contains("branch_product_availability", sql);
            Assert.Contains("is_available", sql);
            Assert.Contains(command.Values, value => Equals(value, BranchId));
        }

        [Fact]
        public async Task SearchEscapesWildcardCharacters()
        {
            var executed = Capture();
            await using var context = CreateContext(executed);
            var store = new GuestMenuStore(context);

            await store.ListAsync(
                BranchId, @"%_\", null, null, null, null, 1, 20, DefaultSort(), false, default);

            var command = Assert.Single(executed.Commands);
            Assert.Contains("ILIKE", command.Sql);
            Assert.Contains("ESCAPE", command.Sql);
            Assert.Contains(command.Values, value => Equals(value, @"%\%\_\\%"));
        }

        [Fact]
        public async Task CategoryKindCaffeineAndTagFiltersUseBoundParameters()
        {
            var executed = Capture();
            await using var context = CreateContext(executed);
            var store = new GuestMenuStore(context);
            var categoryId = Guid.NewGuid();

            await store.ListAsync(
                BranchId, " LATTE ", categoryId, ProductKind.Drink, true, " Hot ", 1, 20, DefaultSort(), false,
                default);

            var command = Assert.Single(executed.Commands);
            var sql = command.Sql.Replace("\"", string.Empty);
            Assert.Contains("product_tags", sql);
            Assert.Contains("kind =", sql);
            Assert.Contains("caffeine", sql);
            Assert.Contains(command.Values, value => Equals(value, "hot"));
            Assert.Contains(command.Values, value => Equals(value, "%LATTE%"));
            Assert.Contains(command.Values, value => Equals(value, categoryId));
        }

        [Fact]
        public async Task PagingFetchesOneExtraRowAndAppliesTheRequestedSort()
        {
            var executed = Capture();
            await using var context = CreateContext(executed);
            var store = new GuestMenuStore(context);

            await store.ListAsync(
                BranchId, null, null, null, null, null, 3, 10,
                [new("name", SortDirection.Desc), new("id", SortDirection.Desc)], true, default);

            Assert.Equal(2, executed.Commands.Count);
            var command = executed.Commands[^1];
            var sql = command.Sql.Replace("\"", string.Empty);
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("name DESC", sql);
            Assert.Contains(command.Values, value => Equals(value, 20));
            Assert.Contains(command.Values, value => Equals(value, 11));
        }

        [Fact]
        public async Task VisibleProductsUseTheGuestMenuVisibilityRules()
        {
            var executed = Capture();
            await using var context = CreateContext(executed);
            var store = new GuestMenuStore(context);

            var products = await store.ListVisibleAsync(BranchId, default);

            Assert.Empty(products);
            var command = Assert.Single(executed.Commands);
            var sql = command.Sql.Replace("\"", string.Empty);
            Assert.Contains("FROM products", sql);
            Assert.Contains("status = 'Active'", sql);
            Assert.Contains("menu_categories", sql);
            Assert.Contains("parent_id IS NULL", sql);
            Assert.Contains("branch_product_availability", sql);
            Assert.Contains("is_available", sql);
            Assert.Contains(command.Values, value => Equals(value, BranchId));
        }

        [Fact]
        public async Task VisibleSearchEscapesWildcardsAndAppliesTheLimit()
        {
            var executed = Capture();
            await using var context = CreateContext(executed);
            var store = new GuestMenuStore(context);

            await store.SearchVisibleAsync(BranchId, @"%_\", 5, default);

            var command = Assert.Single(executed.Commands);
            Assert.Contains("ILIKE", command.Sql);
            Assert.Contains("ESCAPE", command.Sql);
            Assert.Contains(command.Values, value => Equals(value, @"%\%\_\\%"));
            Assert.Contains(command.Values, value => Equals(value, 5));
        }

        [Fact]
        public async Task VisibleSearchRejectsANonPositiveLimit()
        {
            await using var context = CreateContext(Capture());
            var store = new GuestMenuStore(context);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => store.SearchVisibleAsync(BranchId, "latte", 0, default));
        }

        private static CapturedCommands Capture() => new();

        private static CafeHub.Infrastructure.Persistence.Tenant.TenantDbContext CreateContext(
            CapturedCommands executed)
        {
            var stub = new TenantCommandStub((command, _) =>
            {
                executed.Commands.Add(new ExecutedCommand(
                    command.CommandText,
                    command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).ToArray()));
                return command.CommandText.Contains("count(*)", StringComparison.OrdinalIgnoreCase)
                    ? TenantCommandStub.Scalars(0)
                    : new DataTable();
            });
            return stub.CreateContext();
        }

        private static IReadOnlyList<SortTerm> DefaultSort() =>
            [new("sortOrder", SortDirection.Asc), new("id", SortDirection.Asc)];

        private sealed class CapturedCommands
        {
            public List<ExecutedCommand> Commands { get; } = [];
        }

        private sealed record ExecutedCommand(string Sql, object?[] Values);
    }
}
