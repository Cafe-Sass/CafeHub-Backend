using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Commands;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests.Staff.Commands
{
    public sealed class ChangePasswordCommandHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private readonly FakeStaffStore store = new();
        private readonly FakeStaffCurrentTenantUser currentUser = new();
        private readonly StubTenantContextAccessor contextAccessor = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly ChangePasswordCommandHandler handler;

        public ChangePasswordCommandHandlerTests()
        {
            handler = new ChangePasswordCommandHandler(store, currentUser, contextAccessor);
        }

        [Fact]
        public async Task Handle_ChangesPasswordSuccessfully_AndRevokesTokens()
        {
            var result = await handler.Handle(new ChangePasswordCommand("OldPass123!", "NewPass456!"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(store.ChangePasswordCalled);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenCurrentPasswordIsWrong()
        {
            store.Failure = StaffErrors.InvalidCurrentPassword();

            var result = await handler.Handle(new ChangePasswordCommand("WrongPass!", "NewPass456!"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            var validationError = Assert.IsType<ValidationError>(result.Error);
            Assert.Contains(validationError.Errors, e => e.Field == "currentPassword");
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenNotAuthenticated()
        {
            currentUser.IsAuthenticated = false;

            var result = await handler.Handle(new ChangePasswordCommand("OldPass123!", "NewPass456!"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.False(store.ChangePasswordCalled);
        }

        [Fact]
        public async Task Handle_ReturnsFailure_WhenTenantMismatch()
        {
            currentUser.TenantId = Guid.NewGuid();

            var result = await handler.Handle(new ChangePasswordCommand("OldPass123!", "NewPass456!"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.False(store.ChangePasswordCalled);
        }
    }
}
