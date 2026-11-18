using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Tenancy;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Menu.Commands
{
    public sealed class ProductStationAssignmentHandlerTests
    {
        [Theory]
        [InlineData("other-tenant")]
        [InlineData("other-branch")]
        [InlineData("inactive-branch")]
        public async Task RejectsOutOfScopeReadsAndWritesBeforeAccessingTenantData(string scope)
        {
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var tenant = new StubTenantContextAccessor(TenantContext.Create(tenantId, "fixture", true, "fixture-cafe"));
            var actor = new FakeTask42Access
            {
                TenantId = scope == "other-tenant" ? Guid.NewGuid() : tenantId,
                AccountAccess = new TenantAccountAccess(true, "fixture", scope != "inactive-branch")
                {
                    BranchId = scope == "other-branch" ? Guid.NewGuid() : branchId,
                },
            };
            var store = new UnreachableStore();
            var read = await new GetProductStationAssignmentQueryHandler(store, actor, tenant)
                .Handle(new GetProductStationAssignmentQuery(productId, branchId), default);
            var write = await new SetProductStationAssignmentCommandHandler(store, actor, tenant)
                .Handle(new SetProductStationAssignmentCommand(productId, branchId, null, "token"), default);

            var expected = scope == "other-branch" ? ProductErrors.NotFound() : AuthErrors.Forbidden();
            Assert.Equal(expected, read.Error);
            Assert.Equal(expected, write.Error);
        }

        private sealed class UnreachableStore : IProductStationAssignmentStore
        {
            public Task<Result<ProductStationAssignmentResponse>> GetAsync(
                Guid productId, Guid branchId, CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result<ProductStationAssignmentResponse>> SetAsync(
                Guid productId, Guid branchId, Guid? stationId, string token, Guid actorId,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result<ProductRouting>> ResolveRoutingAsync(
                Guid productId, Guid branchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        }
    }
}
