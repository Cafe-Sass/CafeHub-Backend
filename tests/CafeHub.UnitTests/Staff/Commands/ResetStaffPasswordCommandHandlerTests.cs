using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class ResetStaffPasswordCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid StaffId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        private readonly FakeStaffStore store = new();
        private readonly FakeStaffCurrentTenantUser currentUser = new();
        private readonly StubTenantContextAccessor contextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly ResetStaffPasswordCommandHandler handler;

        public ResetStaffPasswordCommandHandlerTests()
        {
            handler = new ResetStaffPasswordCommandHandler(store, currentUser, contextAccessor);
        }

        [Fact]
        public async Task Handle_ResetsPasswordAndRevokesTokens()
        {
            var result = await handler.Handle(new ResetStaffPasswordCommand(StaffId, "NewPass123!"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Contains(StaffId, store.ResetPasswordCalls);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenStaffNotFound()
        {
            store.Failure = StaffErrors.NotFound();

            var result = await handler.Handle(new ResetStaffPasswordCommand(StaffId, "NewPass123!"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(StaffErrors.NotFoundCode, result.Error.Code);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotAuthenticated()
        {
            currentUser.IsAuthenticated = false;

            var result = await handler.Handle(new ResetStaffPasswordCommand(StaffId, "NewPass123!"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(store.ResetPasswordCalls);
        }
    }
}
