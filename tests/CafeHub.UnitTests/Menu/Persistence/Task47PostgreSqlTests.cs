using System.Globalization;
using System.Text.Json;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Menu;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class Task47PostgreSqlTests
    {
        [Task47PostgreSqlFact]
        public async Task RemediationPreservesLegacyRowsAndEnforcesLiveRoutingAndActiveDefault()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var context = database.CreateContext();
            Assert.Equal(0, await context.Database.SqlQueryRaw<int>(
                """SELECT COUNT(*)::int AS "Value" FROM pg_tables WHERE schemaname = 'public'""").SingleAsync());

            var migrator = context.GetService<IMigrator>();
            var applicator = new NpgsqlTenantMigrationApplicator(new() { AdminConnectionString = database.ConnectionString });
            await applicator.ApplyAsync(database.Name, default);

            var now = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);
            var actor = TenantUser.Create(Guid.CreateVersion7(), TenantUserKind.Owner, "fixture-owner", "FIXTURE-OWNER",
                "Fixture Owner", "owner@example.test", "OWNER@EXAMPLE.TEST", null, "fixture-hash", "fixture-stamp", "fixture-version", now);
            var branch = Branch.Create(Guid.CreateVersion7(), "North", BranchLifecycleStatus.Active, null, null, null, now);
            var secondBranch = Branch.Create(Guid.CreateVersion7(), "South", BranchLifecycleStatus.Active, null, null, null, now);
            var station = Station.Create(Guid.CreateVersion7(), branch.Id, "Bar", 0, now);
            var category = MenuCategory.Create(Guid.CreateVersion7(), null, "Coffee", 0, now, actor.Id);
            var newDefault = ProductSize.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Small", 0m, false, now, actor.Id);
            var oldDefault = ProductSize.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Large", 20m, true, now, actor.Id);
            var product = Product.Create(Guid.CreateVersion7(), category.Id, station.Id, "Coffee 100%", null, null,
                ProductKind.Drink, true, null, 0, [newDefault, oldDefault],
                [ProductAddon.Create(Guid.CreateVersion7(), "Milk", 2m, now, actor.Id),
                 ProductAddon.Create(Guid.CreateVersion7(), "Shot", 3m, now, actor.Id)], [], now, actor.Id);
            context.AddRange(actor, branch, secondBranch, station, category, product);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO product_tags (product_id, tag, created_at) VALUES ({product.Id}, {"warm"}, {now})");

            await using var runtime = database.CreateContext();
            await runtime.Database.OpenConnectionAsync();
            await Task47PostgreSqlDatabase.ExecuteAsync((NpgsqlConnection)runtime.Database.GetDbConnection(),
                $"SET ROLE {Task47PostgreSqlDatabase.Quote(database.ApplicationRole)}");
            var runtimeStore = new ProductStore(runtime, new RecordingTenantAuditWriter(), TimeProvider.System,
                new ProductCatalogSchema(runtime));
            var catalogError = ProductErrors.CatalogSchemaNotReady();
            Assert.Equal(catalogError, (await runtimeStore.GetAsync(product.Id, default)).Error);
            Assert.Equal(catalogError, (await runtimeStore.ListAsync(null, null, null, "warm", null, null, 1, 10,
                [new("id", SortDirection.Asc)], true, default)).Error);
            var initialModel = new ProductWriteModel(category.Id, null, product.Name, null, null,
                ProductKind.Drink, true, null, 0, [new(oldDefault.Id, "Large", 20m, true)], [], []);
            Assert.Equal(catalogError, (await runtimeStore.CreateAsync(initialModel, actor.Id, default)).Error);
            Assert.Equal(catalogError, (await runtimeStore.UpdateAsync(product.Id, initialModel, 0, actor.Id, default)).Error);
            Assert.Equal(catalogError, (await runtimeStore.ChangeStatusAsync(product.Id, ProductStatus.Inactive, 0, actor.Id, default)).Error);

            await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("FinalizeTask47ProductTagIdentity"));
            var tagId = Guid.CreateVersion7();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE product_tags SET id = {tagId} WHERE product_id = {product.Id}");
            await migrator.MigrateAsync("FinalizeTask47ProductTagIdentity");
            Assert.Equal(tagId, (await context.ProductTags.AsNoTracking().SingleAsync()).Id);
            Assert.Equal(station.Id, (await context.Products.AsNoTracking().SingleAsync()).StationId);
            var readyCatalog = await runtimeStore.GetAsync(product.Id, default);
            Assert.True(readyCatalog.IsSuccess);
            Assert.Equal(tagId, Assert.Single(readyCatalog.Value.Tags).Id);

            context.ChangeTracker.Clear();
            var clock = new FixedTimeProvider(now.AddMinutes(1));
            var tenantId = Guid.NewGuid();
            var audit = new TenantAuditWriter(
                new FakeCurrentTenantUser { TenantId = tenantId, UserId = actor.Id },
                clock, context, new StubTenantContextAccessor(TenantContext.Create(tenantId, "fixture", true)));
            var routing = new ProductStationAssignmentStore(context, audit, clock);
            Assert.Equal(ProductErrors.RoutingNotReady(), (await routing.ResolveRoutingAsync(product.Id, branch.Id, default)).Error);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO product_routing_readiness (singleton, verified_at) VALUES (TRUE, {now})");
            var first = await routing.GetAsync(product.Id, branch.Id, default);
            var other = await routing.GetAsync(product.Id, secondBranch.Id, default);
            Assert.Equal(ProductRoutingState.Unassigned, (await routing.ResolveRoutingAsync(product.Id, secondBranch.Id, default)).Value.State);
            await context.Products.Where(row => row.Id == product.Id).ExecuteUpdateAsync(setters =>
                setters.SetProperty(row => row.Status, ProductStatus.Inactive));
            var inactiveRouting = (await routing.ResolveRoutingAsync(product.Id, secondBranch.Id, default)).Value;
            Assert.Equal(ProductRoutingState.Unassigned, inactiveRouting.State);
            Assert.False(inactiveRouting.ProductAvailable);
            Assert.False(inactiveRouting.CanOrder);
            Assert.Equal(other.Value, (await routing.GetAsync(product.Id, secondBranch.Id, default)).Value);
            await context.Products.Where(row => row.Id == product.Id).ExecuteUpdateAsync(setters =>
                setters.SetProperty(row => row.Status, ProductStatus.Active));
            Assert.True((await routing.ResolveRoutingAsync(product.Id, secondBranch.Id, default)).Value.CanOrder);

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                var assigned = await routing.SetAsync(product.Id, branch.Id, station.Id, first.Value.ConcurrencyToken, actor.Id, default);
                Assert.True(assigned.IsSuccess);
                Assert.NotNull(assigned.Value.Assignment);
                await transaction.CommitAsync();
            }

            Assert.Equal(other.Value.ConcurrencyToken,
                (await routing.GetAsync(product.Id, secondBranch.Id, default)).Value.ConcurrencyToken);
            await context.Stations.Where(row => row.Id == station.Id).ExecuteUpdateAsync(setters =>
                setters.SetProperty(row => row.Status, StationStatus.Inactive));
            Assert.Equal(ProductRoutingState.StationInactive, (await routing.ResolveRoutingAsync(product.Id, branch.Id, default)).Value.State);
            var suspended = (await routing.GetAsync(product.Id, branch.Id, default)).Value;
            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                var repeated = await routing.SetAsync(product.Id, branch.Id, station.Id,
                    suspended.ConcurrencyToken, actor.Id, default);
                Assert.True(repeated.IsSuccess);
                Assert.Equal(suspended, repeated.Value);
                await transaction.CommitAsync();
            }

            Assert.Single(await context.TenantAuditRecords.Where(row => row.ActionCode == "products.station_assignment.changed").ToArrayAsync());
            await context.Stations.Where(row => row.Id == station.Id).ExecuteUpdateAsync(setters =>
                setters.SetProperty(row => row.Status, StationStatus.Active));
            Assert.Equal(station.Id, (await routing.ResolveRoutingAsync(product.Id, branch.Id, default)).Value.StationId);
            Assert.Single(await context.ProductStationAssignments.AsNoTracking().ToArrayAsync());

            var store = new ProductStore(context, audit, clock, new ProductCatalogSchema(context));
            var before = await store.GetAsync(product.Id, default);
            Assert.True(before.IsSuccess);
            var milk = product.Addons.Single(addon => addon.Name == "Milk");
            var shot = product.Addons.Single(addon => addon.Name == "Shot");
            var model = new ProductWriteModel(
                category.Id, null, product.Name, new string('م', 1000), null, ProductKind.Drink, true, null, 0,
                [new ProductSizeInput(newDefault.Id, "Large", 0m, true), new ProductSizeInput(oldDefault.Id, "Small", 20m, false)],
                [new ProductAddonInput(milk.Id, "Shot", 4m), new ProductAddonInput(shot.Id, "Milk", 3m)],
                [new ProductTagInput(tagId, "hot"), new ProductTagInput(null, "coffee")]);
            var originalVersion = uint.Parse(before.Value.ConcurrencyToken, CultureInfo.InvariantCulture);
            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                var update = await store.UpdateAsync(product.Id, model, originalVersion, actor.Id, default);
                Assert.True(update.IsSuccess, update.Error?.Code);
                Assert.Equal("Large", update.Value.Sizes.Single(size => size.Id == newDefault.Id).Name);
                Assert.Equal("Shot", update.Value.Addons.Single(addon => addon.Id == milk.Id).Name);
                Assert.Contains(update.Value.Tags, tag => tag.Id == tagId && tag.Tag == "hot");
                await transaction.RollbackAsync();
            }

            context.ChangeTracker.Clear();
            Assert.Equal("Small", (await context.ProductSizes.AsNoTracking().SingleAsync(size => size.Id == newDefault.Id)).Name);
            Assert.Equal("Milk", (await context.ProductAddons.AsNoTracking().SingleAsync(addon => addon.Id == milk.Id)).Name);
            Assert.False(await context.TenantAuditRecords.AnyAsync(row => row.ActionCode == "products.updated.succeeded"));

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                var update = await store.UpdateAsync(product.Id, model, originalVersion, actor.Id, default);
                Assert.True(update.IsSuccess, update.Error?.Code);
                Assert.Equal("Shot", update.Value.Addons.Single(addon => addon.Id == milk.Id).Name);
                await transaction.CommitAsync();
            }

            var persisted = await context.TenantAuditRecords.AsNoTracking()
                .Where(row => row.ActionCode == "products.updated.succeeded").ToArrayAsync();
            var beforeParts = persisted.Select(row => JsonSerializer.Deserialize<JsonElement>(row.BeforeState!)).ToArray();
            var afterParts = persisted.Select(row => JsonSerializer.Deserialize<JsonElement>(row.AfterState!)).ToArray();
            var changeSet = Assert.Single(afterParts.Select(part => part.GetProperty("changeSetId").GetGuid()).Distinct());
            Assert.All(beforeParts, part => Assert.Equal(changeSet, part.GetProperty("changeSetId").GetGuid()));
            var beforeDescription = Assert.Single(beforeParts, part => part.GetProperty("field").GetString() == "Description"
                && part.GetProperty("fragment").GetInt32() == 0);
            Assert.True(beforeDescription.GetProperty("present").GetBoolean());
            Assert.Equal(JsonValueKind.Null, beforeDescription.GetProperty("value").ValueKind);
            var addedLabel = Assert.Single(beforeParts, part => part.GetProperty("field").GetString()!.StartsWith("tag.", StringComparison.Ordinal)
                && part.GetProperty("field").GetString()!.EndsWith(".label", StringComparison.Ordinal)
                && !part.GetProperty("present").GetBoolean());
            Assert.Equal(0, addedLabel.GetProperty("fragmentCount").GetInt32());

            Assert.Equal(newDefault.Id, (await context.ProductSizes.AsNoTracking()
                .SingleAsync(row => row.IsDefault && row.Status == ProductComponentStatus.Active)).Id);
            Assert.Equal("hot", (await context.ProductTags.AsNoTracking().SingleAsync(tag => tag.Id == tagId)).Tag);

            var withDefault = await ListMatchingProductAsync();
            Assert.Equal(0m, Assert.Single(withDefault.Value.Items).DefaultSizePrice);

            var afterUpdate = (await runtimeStore.GetAsync(product.Id, default)).Value;
            var withoutTags = await runtimeStore.UpdateAsync(product.Id, model with { Tags = [] },
                uint.Parse(afterUpdate.ConcurrencyToken, CultureInfo.InvariantCulture), actor.Id, default);
            Assert.True(withoutTags.IsSuccess);
            Assert.Empty(withoutTags.Value.Tags);
            var readded = await runtimeStore.UpdateAsync(product.Id, model with { Tags = [new(null, "hot")] },
                uint.Parse(withoutTags.Value.ConcurrencyToken, CultureInfo.InvariantCulture), actor.Id, default);
            Assert.True(readded.IsSuccess, readded.Error?.Code);
            var replacement = Assert.Single(readded.Value.Tags);
            Assert.NotEqual(tagId, replacement.Id);
            Assert.Equal(ProductComponentStatus.Inactive,
                (await context.ProductTags.AsNoTracking().SingleAsync(row => row.Id == tagId)).Status);
            await context.ProductSizes.Where(row => row.Id == newDefault.Id).ExecuteUpdateAsync(setters =>
                setters.SetProperty(row => row.Status, ProductComponentStatus.Inactive));
            Assert.Null(Assert.Single((await ListMatchingProductAsync()).Value.Items).DefaultSizePrice);
            var withoutDefault = (await routing.ResolveRoutingAsync(product.Id, branch.Id, default)).Value;
            Assert.Equal(ProductRoutingState.Assigned, withoutDefault.State);
            Assert.Equal(station.Id, withoutDefault.StationId);
            Assert.False(withoutDefault.ProductAvailable);
            Assert.False(withoutDefault.CanOrder);

            Task<Result<OffsetPage<ProductListItemResponse>>> ListMatchingProductAsync() =>
                store.ListAsync(null, null, null, "hot", null, "%", 1, 10,
                    [new("id", SortDirection.Asc)], false, default);
        }
    }
}
