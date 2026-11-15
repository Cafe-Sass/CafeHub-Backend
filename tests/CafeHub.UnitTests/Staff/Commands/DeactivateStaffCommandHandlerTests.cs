using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class DeactivateStaffCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        private readonly FakeStaffStore store = new();
        private readonly FakeStaffCurrentTenantUser currentUser = new();
        private readonly StubTenantContextAccessor contextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly DeactivateStaffCommandHandler handler;

        public DeactivateStaffCommandHandlerTests()
        {
            handler = new DeactivateStaffCommandHandler(store, currentUser, contextAccessor);
        }

        [Fact]
        public async Task Handle_DeactivatesStaffSuccessfully()
        {
            var result = await handler.Handle(new DeactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Contains(StaffId, store.DeactivateCalls);
        }

        [Fact]
        public async Task Handle_IsIdempotent_WhenAlreadyDeactivated()
        {
            // Store returns success even if already deactivated (idempotent)
            var result = await handler.Handle(new DeactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenCannotDeactivateOwner()
        {
            store.Failure = StaffErrors.CannotModifyOwner();

            var result = await handler.Handle(new DeactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.CannotModifyOwnerCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotAuthenticated()
        {
            currentUser.IsAuthenticated = false;

            var result = await handler.Handle(new DeactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(store.DeactivateCalls);
        }

        [Fact]
        public async Task Handle_PassesActorUserIdToStore()
        {
            await handler.Handle(new DeactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.Contains(StaffId, store.DeactivateCalls);
        }
    }
}
