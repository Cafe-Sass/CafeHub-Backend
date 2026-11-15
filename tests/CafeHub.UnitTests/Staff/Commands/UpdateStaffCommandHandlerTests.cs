using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class UpdateStaffCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeStaffStore store = new();
        private readonly FakeStaffCurrentTenantUser currentUser = new();
        private readonly StubTenantContextAccessor contextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly UpdateStaffCommandHandler handler;

        public UpdateStaffCommandHandlerTests()
        {
            handler = new UpdateStaffCommandHandler(store, currentUser, contextAccessor);
        }

        [Fact]
        public async Task Handle_UpdatesStaffAndReturnsResponse()
        {
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", "+966500000002", BranchId, "12345");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Updated);
            Assert.Equal(StaffId, call.StaffId);
            Assert.Equal("Jane Smith", call.FullName);
            Assert.Equal("+966500000002", call.PhoneNumber);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal("12345", call.ConcurrencyToken);
            Assert.Equal(UserId, call.ActorUserId);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenStoreReturnsConcurrencyConflict()
        {
            store.Failure = StaffErrors.ConcurrencyConflict();
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, BranchId, "12345");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.ConcurrencyConflictCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenStoreReturnsNotFound()
        {
            store.Failure = StaffErrors.NotFound();
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, BranchId, "12345");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.NotFoundCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenStoreReturnsCannotModifyOwner()
        {
            store.Failure = StaffErrors.CannotModifyOwner();
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, BranchId, "12345");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.CannotModifyOwnerCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotAuthenticated()
        {
            currentUser.IsAuthenticated = false;
            var command = new UpdateStaffCommand(StaffId, "Jane Smith", null, BranchId, "12345");

            var result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(store.Updated);
        }
    }
}
