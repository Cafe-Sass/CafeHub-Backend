using System.Data.Common;
using System.Globalization;
using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Menu;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.UnitTests.Branches;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class Task47LifecyclePostgreSqlTests
    {
        private const string Expansion = "20261117233117_ExpandTask47ProductRemediation";
        private const string Finalization = "20261117233501_FinalizeTask47ProductTagIdentity";
        private static readonly DateTimeOffset Now = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

        [Task47PostgreSqlFact]
        public async Task HiddenAndMissingLifecycleRequestsUseTheSameSqlWithoutWaitingOnTheProductLock()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var admin = database.CreateContext();
            var (actorId, productId) = await SeedHiddenProductAsync(admin);
            var token = await ReadTokenAsync(admin, productId);
            var missingId = Guid.NewGuid();

            var commands = new RecordingCommands();
            await using var runtime = CreateRuntimeContext(database, commands);
            await runtime.Database.OpenConnectionAsync();
            await runtime.Database.ExecuteSqlRawAsync("SET lock_timeout = '1s'");
            await using var services = CreateServices(runtime, actorId);
            var controller = CreateController(services);

            await using (var locker = database.CreateConnection())
            {
                await locker.OpenAsync();
                await using var transaction = await locker.BeginTransactionAsync();
                await using var hold = new NpgsqlCommand("SELECT id FROM products WHERE id = @id FOR UPDATE", locker, transaction);
                hold.Parameters.AddWithValue("id", productId);
                Assert.Equal(productId, await hold.ExecuteScalarAsync());

                foreach (var activate in new[] { false, true })
                {
                    string[]? expectedSql = null;
                    foreach (var (target, version) in new[] { (missingId, token), (productId, token), (productId, "0") })
                    {
                        commands.Sql.Clear();
                        var response = await RunLifecycleAsync(controller, activate, target, version);
                        AssertProblem(response, StatusCodes.Status404NotFound, ProductErrors.NotFound().Code);
                        AssertNotFoundSql(commands.Sql);
                        expectedSql ??= commands.Sql.ToArray();
                        Assert.Equal(expectedSql.AsEnumerable(), commands.Sql);
                    }
                }

                await transaction.RollbackAsync();
            }

            Assert.Equal(ProductStatus.Active, await admin.Products.Where(product => product.Id == productId)
                .Select(product => product.Status).SingleAsync());
            Assert.Equal(token, await ReadTokenAsync(admin, productId));
            Assert.Empty(await admin.TenantAuditRecords.ToListAsync());
        }

        [Task47PostgreSqlFact]
        public async Task AuthorizedExplicitHiddenLifecyclePreservesSuccessStaleConflictsAndNoOps()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var admin = database.CreateContext();
            var (actorId, productId) = await SeedHiddenProductAsync(admin);
            var commands = new RecordingCommands();
            await using var runtime = CreateRuntimeContext(database, commands);
            await using var services = CreateServices(runtime, actorId);
            var controller = CreateController(services);
            var token = await ReadTokenAsync(admin, productId);

            foreach (var activate in new[] { false, true })
            {
                var status = activate ? ProductStatus.Active : ProductStatus.Inactive;
                commands.Sql.Clear();
                var stale = await RunLifecycleAsync(controller, activate, productId, "0", includeHiddenCategories: true);
                AssertProblem(stale, StatusCodes.Status409Conflict, ProductErrors.ConcurrencyConflict().Code);
                AssertSingleSchemaCheck(commands.Sql);

                commands.Sql.Clear();
                var success = await RunLifecycleAsync(controller, activate, productId, token, includeHiddenCategories: true);
                var updated = Assert.IsType<ProductResponse>(Assert.IsType<OkObjectResult>(success.Result).Value);
                Assert.Equal(status.ToString(), updated.Status);
                Assert.NotEqual(token, updated.ConcurrencyToken);
                AssertSingleSchemaCheck(commands.Sql);
                Assert.Equal(status, await admin.Products.Where(product => product.Id == productId)
                    .Select(product => product.Status).SingleAsync());

                token = updated.ConcurrencyToken;
                var repeated = await RunLifecycleAsync(controller, activate, productId, token, includeHiddenCategories: true);
                var unchanged = Assert.IsType<ProductResponse>(Assert.IsType<OkObjectResult>(repeated.Result).Value);
                Assert.Equal(token, unchanged.ConcurrencyToken);
            }

            var audits = await admin.TenantAuditRecords.Select(record => record.ActionCode).ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.Contains("products.deactivated.succeeded", audits);
            Assert.Contains("products.reactivated.succeeded", audits);
        }

        [Task47PostgreSqlFact]
        public async Task DowngradeBetweenBothLifecycleCommandsAndResponseMappingKeepsTheOriginalNotFoundResult()
        {
            await using var database = await Task47PostgreSqlDatabase.CreateAsync();
            await using var admin = database.CreateContext();
            var (actorId, productId) = await SeedHiddenProductAsync(admin);
            var migrator = admin.GetService<IMigrator>();
            var commands = new RecordingCommands();
            await using var runtime = CreateRuntimeContext(database, commands);
            var downgrades = 0;
            await using var services = CreateServices(runtime, actorId, async cancellationToken =>
            {
                Assert.Null(runtime.Database.CurrentTransaction);
                AssertNotFoundSql(commands.Sql);
                await migrator.MigrateAsync(Expansion, cancellationToken);
                downgrades++;
            });
            var controller = CreateController(services);

            foreach (var activate in new[] { false, true })
            {
                foreach (var target in new[] { productId, Guid.NewGuid() })
                {
                    await migrator.MigrateAsync(Finalization);
                    commands.Sql.Clear();
                    var response = await RunLifecycleAsync(controller, activate, target, "0");

                    AssertProblem(response, StatusCodes.Status404NotFound, ProductErrors.NotFound().Code);
                    Assert.Equal(Expansion, (await admin.Database.GetAppliedMigrationsAsync()).Last());
                    AssertNotFoundSql(commands.Sql);
                }
            }

            Assert.Equal(4, downgrades);
        }

        private static async Task<(Guid ActorId, Guid ProductId)> SeedHiddenProductAsync(TenantDbContext admin)
        {
            await admin.GetService<IMigrator>().MigrateAsync(Finalization);
            var actor = TenantUser.Create(Guid.CreateVersion7(), TenantUserKind.Owner, "fixture-owner", "FIXTURE-OWNER",
                "Fixture Owner", "owner@example.test", "OWNER@EXAMPLE.TEST", null, "fixture-hash", "fixture-stamp", "fixture-version", Now);
            var root = MenuCategory.Create(Guid.CreateVersion7(), null, "Drinks", 0, Now, actor.Id);
            var child = MenuCategory.Create(Guid.CreateVersion7(), root, "Coffee", 0, Now, actor.Id);
            var size = ProductSize.Create(Guid.CreateVersion7(), "Regular", 10m, true, Now, actor.Id);
            var product = Product.Create(Guid.CreateVersion7(), child.Id, null, "Coffee", null, null,
                ProductKind.Drink, true, null, 0, [size], [], [], Now, actor.Id);
            root.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, actor.Id);
            admin.AddRange(actor, root, child, product);
            await admin.SaveChangesAsync();
            return (actor.Id, product.Id);
        }

        private static async Task<string> ReadTokenAsync(TenantDbContext context, Guid id)
        {
            var version = await context.Products.Where(product => product.Id == id)
                .Select(product => EF.Property<uint>(product, "xmin")).SingleAsync();
            return version.ToString(CultureInfo.InvariantCulture);
        }

        private static TenantDbContext CreateRuntimeContext(Task47PostgreSqlDatabase database, RecordingCommands commands) =>
            new(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(database.ConnectionString).AddInterceptors(commands).Options);

        private static ServiceProvider CreateServices(
            TenantDbContext runtime, Guid actorId, Func<CancellationToken, Task>? afterTransaction = null)
        {
            var tenantId = Guid.NewGuid();
            var tenant = new StubTenantContextAccessor(TenantContext.Create(tenantId, "fixture", true));
            var user = new FakeCurrentTenantUser { IsAuthenticated = true, TenantId = tenantId, UserId = actorId };
            var clock = new FixedTimeProvider(Now.AddMinutes(1));
            var audit = new TenantAuditWriter(user, clock, runtime, tenant);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorizationBuilder().AddPolicy(TenantPermissions.MenuManage, policy => policy.RequireAssertion(_ => true));
            services.AddSingleton<ITenantContextAccessor>(tenant);
            services.AddSingleton<ICurrentTenantUser>(user);
            services.AddSingleton<ITenantUnitOfWork>(new TenantUnitOfWork(runtime));
            services.AddSingleton<IProductStore>(new ProductStore(runtime, audit, clock, new ProductCatalogSchema(runtime)));
            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<DeactivateProductCommandHandler>();
                if (afterTransaction is not null)
                {
                    configuration.AddOpenBehavior(typeof(AfterLifecycleTransaction<,>));
                }

                configuration.AddOpenBehavior(typeof(TenantTransactionBehavior<,>));
            });
            if (afterTransaction is not null)
            {
                services.AddSingleton(afterTransaction);
            }

            return services.BuildServiceProvider();
        }

        private static TenantProductsController CreateController(IServiceProvider services) =>
            new(services.GetRequiredService<ISender>(), services.GetRequiredService<IAuthorizationService>(), new CorrelationStub())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } },
            };

        private static Task<ActionResult<ProductResponse>> RunLifecycleAsync(
            TenantProductsController controller, bool activate, Guid id, string token, bool includeHiddenCategories = false)
        {
            var request = new ProductLifecycleRequest(token);
            return activate
                ? controller.Reactivate("fixture-cafe", id, request, default, includeHiddenCategories)
                : controller.Deactivate("fixture-cafe", id, request, default, includeHiddenCategories);
        }

        private static void AssertProblem(ActionResult<ProductResponse> response, int status, string code)
        {
            var result = Assert.IsType<ObjectResult>(response.Result);
            Assert.Equal(status, result.StatusCode);
            Assert.Equal(code, Assert.IsType<ProblemDetails>(result.Value).Extensions["code"]);
        }

        private static void AssertNotFoundSql(IEnumerable<string> commands) =>
            Assert.Collection(commands,
                sql => Assert.Contains("LOCK TABLE public.product_tags IN ACCESS SHARE MODE", sql),
                sql => Assert.Contains("pg_catalog.pg_constraint", sql),
                sql =>
                {
                    Assert.Contains("SELECT EXISTS", sql);
                    Assert.Contains("menu_categories", sql);
                    Assert.DoesNotContain("FOR UPDATE", sql);
                });

        private static void AssertSingleSchemaCheck(IEnumerable<string> commands) =>
            Assert.Single(commands, sql => sql.Contains("pg_catalog.pg_constraint", StringComparison.Ordinal));

        private sealed class CorrelationStub : ICorrelationIdAccessor
        {
            public string? CorrelationId => null;
        }

        private sealed class AfterLifecycleTransaction<TRequest, TResponse>(Func<CancellationToken, Task> afterTransaction)
            : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
        {
            public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            {
                var result = await next(cancellationToken);
                if (request is DeactivateProductCommand or ReactivateProductCommand)
                {
                    await afterTransaction(cancellationToken);
                }

                return result;
            }
        }

        private sealed class RecordingCommands : DbCommandInterceptor
        {
            public List<string> Sql { get; } = [];

            public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Sql.Add(command.CommandText);
                return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Sql.Add(command.CommandText);
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
