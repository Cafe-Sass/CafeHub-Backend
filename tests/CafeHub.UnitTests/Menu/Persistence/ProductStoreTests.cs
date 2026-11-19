using System.Data;
using System.Data.Common;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Menu;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class ProductStoreTests
    {
        [Fact]
        public async Task CatalogSearchPassesLiteralWildcardsAndProjectsOnlyAnActiveDefault()
        {
            var stub = new TenantCommandStub((command, _) =>
            {
                Assert.Contains("ILIKE", command.CommandText);
                Assert.Contains("ESCAPE", command.CommandText);
                Assert.Contains("menu_categories", command.CommandText);
                Assert.Contains("is_default", command.CommandText);
                Assert.Matches(@"FROM product_sizes[\s\S]*is_default[\s\S]*status = 'Active'", command.CommandText);
                Assert.Contains(command.Parameters.Cast<DbParameter>(), parameter => Equals(parameter.Value, @"%\%\_\\%"));
                return new DataTable();
            });
            await using var context = stub.CreateContext();
            var store = new ProductStore(context, new RecordingTenantAuditWriter(), new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.ListAsync(null, null, null, null, null, @"%_\", 1, 10,
                [new("id", SortDirection.Asc)], false, default);

            Assert.Empty(result.Value.Items);
        }

        private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();
        private const uint Version = 13;

        [Theory]
        [InlineData("missing")]
        [InlineData("inactive")]
        [InlineData("parent-inactive")]
        public async Task Create_RejectsAnUnusableCategoryUniformly(string kind)
        {
            var categoryId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var stub = new TenantCommandStub((command, _) => command.CommandText.Contains("parent_id", StringComparison.Ordinal)
                ? kind == "missing" ? new DataTable() : CategoryRow(kind == "parent-inactive" ? "Active" : "Inactive", parentId)
                : kind == "parent-inactive" ? StatusRow("Inactive") : new DataTable(), _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.CreateAsync(Model(categoryId), Actor, default);

            var error = Assert.IsType<ValidationError>(result.Error);
            Assert.Equal("categoryId", Assert.Single(error.Errors).Field);
            Assert.Empty(audit.Entries);
            Assert.Empty(context.Products.Local);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("inactive")]
        public async Task Create_RejectsAnUnusableStationUniformly(string kind)
        {
            var stationId = Guid.NewGuid();
            var stub = new TenantCommandStub(
                (command, _) => command.CommandText.Contains("FROM stations", StringComparison.Ordinal)
                    ? kind == "missing" ? new DataTable() : StatusRow("Inactive")
                    : CategoryRow("Active"),
                _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.CreateAsync(Model(Guid.NewGuid(), stationId), Actor, default);

            var error = Assert.IsType<ValidationError>(result.Error);
            Assert.Equal("stationId", Assert.Single(error.Errors).Field);
            Assert.Empty(audit.Entries);
            Assert.Empty(context.Products.Local);
        }

        [Fact]
        public async Task Create_StagesSizesAddonsAndTagsAndWritesTheCreationAudit()
        {
            var stub = new TenantCommandStub((_, _) => CategoryRow("Active"), _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.CreateAsync(Model(Guid.NewGuid()), Actor, default);

            Assert.True(result.IsSuccess);
            var product = Assert.Single(context.Products.Local);
            Assert.Equal("Active", result.Value.Status);
            Assert.Equal("Regular", Assert.Single(result.Value.Sizes).Name);
            Assert.Equal(10m, result.Value.Sizes.Single().Price);
            Assert.True(result.Value.Sizes.Single().IsDefault);
            Assert.Single(result.Value.Addons);
            Assert.Equal("hot", Assert.Single(result.Value.Tags).Tag);
            Assert.All(product.Sizes, size => Assert.Equal(product.Id, size.ProductId));
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("products.created.succeeded", entry.Action);
            Assert.Equal(product.Id, entry.TargetId);
            Assert.Equal(Actor, entry.ActorUserId);
            Assert.Contains($"\"price.size.{product.Sizes.Single().Id:D}\":\"10\"", TenantAuditSnapshotSerializer.Create("products", entry.Changes!).Value.AfterState);
        }

        [Fact]
        public async Task Update_WithAStaleTokenReturnsAConflictWithoutAuditing()
        {
            var product = CreateProduct();
            var stub = new TenantCommandStub(
                (command, data) => command.CommandText.Contains("FROM products", StringComparison.Ordinal)
                    ? TenantCommandStub.EntityRow(command, data, product, Version)
                    : CategoryRow("Active"),
                _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.UpdateAsync(product.Id, Model(product.CategoryId), Version - 1, Actor, default);

            Assert.Equal(ProductErrors.ConcurrencyConflict(), result.Error);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Update_ReplacesTheComponentsAndAuditsOnlyTheChangedPrice()
        {
            var size = ProductSize.Create(Guid.NewGuid(), "Regular", 10m, true, Now, Actor);
            var addon = ProductAddon.Create(Guid.NewGuid(), "Milk", 2m, Now, Actor);
            var product = CreateProduct(sizes: [size], addons: [addon], tags: ["hot"]);
            var stub = new TenantCommandStub(
                (command, data) => Read(command, data, product, size, addon, product.Tags.Single()),
                _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());
            var model = new ProductWriteModel(
                product.CategoryId,
                null,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                [
                    new ProductSizeInput(size.Id, "Regular", 12.50m, true),
                    new ProductSizeInput(null, "Large", 18m, false),
                ],
                [new ProductAddonInput(addon.Id, "Milk", 2m)],
                [new ProductTagInput(product.Tags.Single().Id, "hot")]);

            var result = await store.UpdateAsync(product.Id, model, Version, Actor, default);

            Assert.True(result.IsSuccess);
            var tracked = Assert.Single(context.Products.Local);
            Assert.Equal(2, tracked.Sizes.Count);
            Assert.Equal(12.50m, tracked.Sizes.Single(candidate => candidate.Id == size.Id).Price);
            Assert.Single(tracked.Addons);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("products.updated.succeeded", entry.Action);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", entry.Changes!).Value;
            Assert.Contains($"\"price.size.{size.Id:D}\":\"10\"", snapshot.BeforeState);
            Assert.Contains($"\"price.size.{size.Id:D}\":\"12.50\"", snapshot.AfterState);
            Assert.Contains($"\"price.size.{tracked.Sizes.Single(item => item.Name == "Large").Id:D}\":\"18\"", snapshot.AfterState);
            Assert.DoesNotContain($"price.addon.{addon.Id:D}", snapshot.AfterState);
        }

        [Theory]
        [InlineData(ProductStatus.Active, ProductStatus.Active, false)]
        [InlineData(ProductStatus.Active, ProductStatus.Inactive, true)]
        [InlineData(ProductStatus.Inactive, ProductStatus.Inactive, false)]
        public async Task ChangeStatus_IsIdempotentAndAuditsRealTransitions(
            ProductStatus initial, ProductStatus requested, bool audits)
        {
            var product = CreateProduct();
            if (initial == ProductStatus.Inactive)
            {
                product.ChangeStatus(ProductStatus.Inactive, Now, Actor);
            }

            var stub = new TenantCommandStub(
                (command, data) => Read(command, data, product), _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.ChangeStatusAsync(product.Id, requested, Version, Actor, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(requested.ToString(), result.Value.Status);
            if (audits)
            {
                var entry = Assert.Single(audit.Entries);
                Assert.Equal("products.deactivated.succeeded", entry.Action);
                var snapshot = TenantAuditSnapshotSerializer.Create("products", entry.Changes!).Value;
                Assert.Equal("{\"Status\":\"Active\"}", snapshot.BeforeState);
                Assert.Equal("{\"Status\":\"Inactive\"}", snapshot.AfterState);
            }
            else
            {
                Assert.Empty(audit.Entries);
            }
        }

        [Fact]
        public async Task ChangeStatus_WithAStaleTokenReturnsAConflict()
        {
            var product = CreateProduct();
            var stub = new TenantCommandStub(
                (command, data) => Read(command, data, product), _ => { });
            var audit = new RecordingTenantAuditWriter();
            await using var context = stub.CreateContext(new SaveStub());
            var store = new ProductStore(context, audit, new FixedTimeProvider(Now), new FinalizedCatalog());

            var result = await store.ChangeStatusAsync(
                product.Id, ProductStatus.Inactive, Version + 1, Actor, default);

            Assert.Equal(ProductErrors.ConcurrencyConflict(), result.Error);
            Assert.Empty(audit.Entries);
        }

        private static DataTable Read(
            DbCommand command,
            CommandEventData data,
            Product product,
            ProductSize? size = null,
            ProductAddon? addon = null,
            ProductTag? tag = null)
        {
            var text = command.CommandText;
            if (text.Contains("SELECT EXISTS", StringComparison.Ordinal) && text.Contains("menu_categories", StringComparison.Ordinal))
            {
                return TenantCommandStub.Scalars(true);
            }

            if (text.Contains("FROM product_sizes", StringComparison.Ordinal))
            {
                return size is null ? new DataTable() : TenantCommandStub.EntityRow(command, data, size, Version);
            }

            if (text.Contains("FROM product_addons", StringComparison.Ordinal))
            {
                return addon is null ? new DataTable() : TenantCommandStub.EntityRow(command, data, addon, Version);
            }

            if (text.Contains("FROM product_tags", StringComparison.Ordinal))
            {
                return tag is null ? new DataTable() : TenantCommandStub.EntityRow(command, data, tag, Version);
            }

            if (text.Contains("FROM products", StringComparison.Ordinal))
            {
                return TenantCommandStub.EntityRow(command, data, product, Version);
            }

            return CategoryRow("Active");
        }

        private static ProductWriteModel Model(Guid categoryId, Guid? stationId = null) =>
            new(
                categoryId,
                stationId,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                [new ProductSizeInput(null, "Regular", 10m, true)],
                [new ProductAddonInput(null, "Milk", 2m)],
                [new ProductTagInput(null, "hot")]);

        private static Product CreateProduct(
            IReadOnlyCollection<ProductSize>? sizes = null,
            IReadOnlyCollection<ProductAddon>? addons = null,
            IEnumerable<string>? tags = null) =>
            Product.Create(
                Guid.CreateVersion7(),
                Guid.NewGuid(),
                null,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                sizes ?? [ProductSize.Create(Guid.CreateVersion7(), "Regular", 10m, true, Now, Actor)],
                addons ?? [],
                tags ?? [],
                Now,
                Actor);

        private static DataTable CategoryRow(string status, Guid? parentId = null)
        {
            var table = new DataTable();
            table.Columns.Add("parent_id", typeof(Guid));
            table.Columns.Add("status", typeof(string));
            table.Rows.Add(parentId ?? (object)DBNull.Value, status);
            return table;
        }

        private static DataTable StatusRow(string status)
        {
            var table = new DataTable();
            table.Columns.Add("status", typeof(string));
            table.Rows.Add(status);
            return table;
        }

        private sealed class SaveStub : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }

        private sealed class FinalizedCatalog : IProductCatalogSchema
        {
            public Task<Result<T>> RunAsync<T>(
                Func<Task<Result<T>>> operation, CancellationToken cancellationToken, bool repeatableRead = false) =>
                operation();
        }
    }
}
