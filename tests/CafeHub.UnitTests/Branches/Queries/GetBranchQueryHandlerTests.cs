using System.Linq;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Branches.Queries;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Branches.Queries
{
    public sealed class GetBranchQueryHandlerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeBranchStore store = new();
        private readonly GetBranchQueryHandler handler;

        public GetBranchQueryHandlerTests()
        {
            handler = new GetBranchQueryHandler(store);
        }

        [Fact]
        public async Task Handle_ReturnsTheBranchWithItsConcurrencyToken()
        {
            var expected = new BranchResponse
            {
                Id = BranchId,
                Name = "Downtown Cafe",
                LifecycleStatus = BranchLifecycleStatus.Active.ToString(),
                Address = "123 Main St",
                ContactPhone = "+1-555-0100",
                OpeningHoursSummary = "Mon-Fri 8am-6pm",
                CreatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "12345",
            };
            store.GetByIdResponse = expected;

            var result = await handler.Handle(new GetBranchQuery(BranchId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Same(expected, result.Value);
            Assert.Equal("12345", result.Value!.ConcurrencyToken);
            Assert.Equal(BranchId, Assert.Single(store.GetByIdRequests));
        }

        [Fact]
        public async Task Handle_WhenTheBranchIsMissing_ReturnsNotFound()
        {
            store.GetByIdResponse = null;

            var result = await handler.Handle(new GetBranchQuery(Guid.NewGuid()), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchesErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
        }

        [Fact]
        public async Task Handle_WithANullQuery_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        [Fact]
        public void TheHandler_RequiresTheStore()
        {
            Assert.Throws<ArgumentNullException>(() => new GetBranchQueryHandler(null!));
        }

        [Fact]
        public void TheQuery_IsAQueryOfTheBranchResponse()
        {
            Assert.True(typeof(IQuery<BranchResponse>).IsAssignableFrom(typeof(GetBranchQuery)));
            Assert.True(
                typeof(IQueryHandler<GetBranchQuery, BranchResponse>)
                    .IsAssignableFrom(typeof(GetBranchQueryHandler)));
        }

        [Fact]
        public void TheResponse_ExposesOnlyContractFieldsAndNoAuditUserMembers()
        {
            var expected = new[]
            {
                "Address", "ConcurrencyToken", "ContactPhone", "CreatedAt", "Id", "LifecycleStatus",
                "Name", "OpeningHoursSummary", "UpdatedAt",
            };

            var properties = typeof(BranchResponse)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                expected.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                properties);
            Assert.Equal(9, properties.Length);
            Assert.DoesNotContain(properties, property => property.Contains("By", StringComparison.OrdinalIgnoreCase));
        }
    }
}
