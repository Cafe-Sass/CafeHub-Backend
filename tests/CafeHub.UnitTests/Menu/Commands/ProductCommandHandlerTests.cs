using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Menu.Commands
{
    public sealed class ProductCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private readonly FakeProductStore store = new() { Response = FakeProductStore.Product() };
        private readonly FakeCurrentTenantUser currentUser = new() { TenantId = TenantId, UserId = ActorId };
        private readonly StubTenantContextAccessor tenantContext = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));

        [Fact]
        public async Task Create_DispatchesTheWriteModelForTheCurrentActor()
        {
            var command = CreateCommand();
            var handler = new CreateProductCommandHandler(store, currentUser, tenantContext);

            var result = await handler.Handle(command, default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Created);
            Assert.Equal(ActorId, call.ActorId);
            Assert.Equal(command.CategoryId, call.Product.CategoryId);
            Assert.Equal(command.StationId, call.Product.StationId);
            Assert.Equal(command.Name, call.Product.Name);
            Assert.Equal(command.Kind, call.Product.Kind);
            Assert.Equal(command.Sizes, call.Product.Sizes);
            Assert.Equal(command.Addons, call.Product.Addons);
            Assert.Equal(command.Tags, call.Product.Tags);
        }

        [Fact]
        public async Task Update_DispatchesTheParsedVersionAndWriteModel()
        {
            var command = UpdateCommand(concurrencyToken: "42");
            var handler = new UpdateProductCommandHandler(store, currentUser, tenantContext);

            var result = await handler.Handle(command, default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Updated);
            Assert.Equal(command.Id, call.Id);
            Assert.Equal(42u, call.Version);
            Assert.Equal(ActorId, call.ActorId);
            Assert.Equal(command.Name, call.Product.Name);
        }

        [Theory]
        [InlineData("deactivate", false)]
        [InlineData("reactivate", false)]
        [InlineData("deactivate", true)]
        [InlineData("reactivate", true)]
        public async Task StatusCommands_ForwardStatusAndVisibility(string operation, bool includeHiddenCategories)
        {
            var id = Guid.NewGuid();
            var deactivate = operation == "deactivate";
            Result<CafeHub.Application.Menu.DTOs.ProductResponse> result;
            if (deactivate)
            {
                var handler = new DeactivateProductCommandHandler(store, currentUser, tenantContext);
                result = await handler.Handle(new DeactivateProductCommand(id, "7", includeHiddenCategories), default);
            }
            else
            {
                var handler = new ReactivateProductCommandHandler(store, currentUser, tenantContext);
                result = await handler.Handle(new ReactivateProductCommand(id, "7", includeHiddenCategories), default);
            }

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.StatusChanges);
            Assert.Equal(id, call.Id);
            Assert.Equal(7u, call.Version);
            Assert.Equal(includeHiddenCategories, call.IncludeHiddenCategories);
            Assert.Equal(deactivate ? ProductStatus.Inactive : ProductStatus.Active, call.Status);
        }

        [Theory]
        [InlineData("unauthenticated")]
        [InlineData("wrong-tenant")]
        [InlineData("missing-user")]
        public async Task Commands_FailWhenTheTenantActorIsUnavailable(string condition)
        {
            switch (condition)
            {
                case "unauthenticated":
                    currentUser.IsAuthenticated = false;
                    break;
                case "wrong-tenant":
                    currentUser.TenantId = Guid.NewGuid();
                    break;
                default:
                    currentUser.UserId = Guid.Empty;
                    break;
            }

            var handler = new CreateProductCommandHandler(store, currentUser, tenantContext);

            var result = await handler.Handle(CreateCommand(), default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailable(), result.Error);
            Assert.Empty(store.Created);
        }

        [Fact]
        public async Task Commands_FailBeforeTheStoreWhenTheConcurrencyTokenIsInvalid()
        {
            var update = new UpdateProductCommandHandler(store, currentUser, tenantContext);
            var updateResult = await update.Handle(UpdateCommand(concurrencyToken: "not-a-version"), default);
            var deactivate = new DeactivateProductCommandHandler(store, currentUser, tenantContext);
            var deactivateResult = await deactivate.Handle(new DeactivateProductCommand(Guid.NewGuid(), " "), default);
            var reactivate = new ReactivateProductCommandHandler(store, currentUser, tenantContext);
            var reactivateResult = await reactivate.Handle(new ReactivateProductCommand(Guid.NewGuid(), "-1"), default);

            Assert.Equal("concurrencyToken", Assert.Single(Assert.IsType<ValidationError>(updateResult.Error).Errors).Field);
            Assert.Equal("concurrencyToken", Assert.Single(Assert.IsType<ValidationError>(deactivateResult.Error).Errors).Field);
            Assert.Equal("concurrencyToken", Assert.Single(Assert.IsType<ValidationError>(reactivateResult.Error).Errors).Field);
            Assert.Empty(store.Updated);
            Assert.Empty(store.StatusChanges);
        }

        internal static CreateProductCommand CreateCommand(
            string name = "Latte",
            IReadOnlyList<ProductSizeInput>? sizes = null,
            IReadOnlyList<string>? tags = null) =>
            new(
                Guid.NewGuid(),
                null,
                name,
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                sizes ?? [new ProductSizeInput(null, "Regular", 10m, true)],
                [new ProductAddonInput(null, "Milk", 2m)],
                (tags ?? ["hot"]).Select(tag => new ProductTagInput(null, tag)).ToArray());

        internal static UpdateProductCommand UpdateCommand(string concurrencyToken = "1") =>
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                [new ProductSizeInput(null, "Regular", 10m, true)],
                [new ProductAddonInput(null, "Milk", 2m)],
                [new ProductTagInput(null, "hot")],
                concurrencyToken);
    }
}
