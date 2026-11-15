using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Application.Staff.Queries;

namespace CafeHub.UnitTests.Staff.Queries
{
    public sealed class GetStaffByIdQueryHandlerTests
    {
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeStaffStore store = new();
        private readonly GetStaffByIdQueryHandler handler;

        public GetStaffByIdQueryHandlerTests()
        {
            handler = new GetStaffByIdQueryHandler(store);
        }

        [Fact]
        public async Task Handle_ReturnsStaffResponse_WhenFound()
        {
            var expected = FakeStaffStore.MakeResponse(StaffId, "Jane Doe", "jane@test.com", null, BranchId);
            store.GetByIdResult = expected;

            var result = await handler.Handle(new GetStaffByIdQuery(StaffId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(StaffId, result.Value.Id);
            Assert.Equal("Jane Doe", result.Value.FullName);
            Assert.Equal("jane@test.com", result.Value.Email);
            Assert.Equal(BranchId, result.Value.BranchId);
        }

        [Fact]
        public async Task Handle_ReturnsNotFound_WhenStaffDoesNotExist()
        {
            store.GetByIdResult = null;

            var result = await handler.Handle(new GetStaffByIdQuery(StaffId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.NotFoundCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_PassesStaffIdToStore()
        {
            await handler.Handle(new GetStaffByIdQuery(StaffId), CancellationToken.None);

            Assert.Contains(StaffId, store.GetByIdRequests);
        }

        [Fact]
        public void TheHandler_RequiresDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new GetStaffByIdQueryHandler(null!));
        }
    }
}
