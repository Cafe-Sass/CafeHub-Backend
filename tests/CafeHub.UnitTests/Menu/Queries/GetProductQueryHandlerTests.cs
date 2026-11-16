using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Queries;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Menu.Commands;

namespace CafeHub.UnitTests.Menu.Queries
{
    public sealed class GetProductQueryHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private readonly FakeProductStore store = new() { Response = FakeProductStore.Product() };

        [Fact]
        public async Task Get_ReturnsTheProductWhenItExists()
        {
            var id = Guid.NewGuid();
            store.Response = FakeProductStore.Product(id);
            var handler = new GetProductQueryHandler(store);

            var result = await handler.Handle(new GetProductQuery(id), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(id, result.Value.Id);
            Assert.Single(store.Gotten);
            Assert.Equal(id, Assert.Single(store.Gotten));
        }

        [Fact]
        public async Task Get_ReturnsNotFoundWhenTheProductIsMissing()
        {
            store.Response = null;
            var handler = new GetProductQueryHandler(store);

            var result = await handler.Handle(new GetProductQuery(Guid.NewGuid()), default);

            Assert.False(result.IsSuccess);
            Assert.Equal("Products.NotFound", result.Error!.Code);
        }
    }
}
