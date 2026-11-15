using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class ReactivateStaffCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        private readonly FakeStaffStore store = new();
        private readonly FakeStaffCurrentTenantUser currentUser = new();
        private readonly StubTenantContextAccessor contextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly ReactivateStaffCommandHandler handler;

        public ReactivateStaffCommandHandlerTests()
        {
            handler = new ReactivateStaffCommandHandler(store, currentUser, contextAccessor);
        }

        [Fact]
        public async Task Handle_ReactivatesStaffSuccessfully()
        {
            var result = await handler.Handle(new ReactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Contains(StaffId, store.ReactivateCalls);
        }

        [Fact]
        public async Task Handle_IsIdempotent_WhenAlreadyActive()
        {
            // Store returns success even if already active (idempotent)
            var result = await handler.Handle(new ReactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotFound()
        {
            store.Failure = StaffErrors.NotFound();

            var result = await handler.Handle(new ReactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.NotFoundCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotAuthenticated()
        {
            currentUser.IsAuthenticated = false;

            var result = await handler.Handle(new ReactivateStaffCommand(StaffId), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(store.ReactivateCalls);
        }
    }
}
