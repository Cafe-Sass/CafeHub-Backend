using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Identity.Queries;
using CafeHub.Domain.Identity;

namespace CafeHub.UnitTests
{
    public sealed class GetCurrentAdminQueryHandlerTests
    {
        private const string AdminEmail = "current.admin@cafehub.app";

        private readonly FakeCurrentUser currentUser = new();
        private readonly StubAdminAccountReader adminAccountReader = new();
        private readonly GetCurrentAdminQueryHandler handler;

        public GetCurrentAdminQueryHandlerTests()
        {
            handler = new GetCurrentAdminQueryHandler(currentUser, adminAccountReader);
        }

        private static AuthenticatedAdmin CreateAdministrator(Guid adminId)
        {
            return new AuthenticatedAdmin
            {
                Id = adminId,
                Email = AdminEmail,
                Roles = new[] { PlatformAdminRoles.SuperAdmin },
            };
        }

        private static void AssertUnavailable(Result<CurrentAdminResponse> result)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.CurrentAdminUnavailableCode, result.Error!.Code);
            Assert.Equal(AuthErrors.CurrentAdminUnavailableMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        }

        [Fact]
        public async Task AnAuthenticatedActiveAdministrator_IsReturnedWithTheStoredRoles()
        {
            var adminId = Guid.NewGuid();
            currentUser.IsAuthenticated = true;
            currentUser.AdminId = adminId;
            currentUser.Roles = new[] { PlatformAdminRoles.SuperAdmin };
            adminAccountReader.NextAdmin = CreateAdministrator(adminId);

            var result = await handler.Handle(new GetCurrentAdminQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value;
            Assert.Equal(adminId, response.Id);
            Assert.Equal(AdminEmail, response.Email);
            Assert.Equal(new[] { PlatformAdminRoles.SuperAdmin }, response.Roles);
            Assert.Equal(new[] { adminId }, adminAccountReader.RequestedAdminIds);
        }

        [Fact]
        public async Task TheQuery_ForwardsTheCancellationToken_ToTheAccountReader()
        {
            var adminId = Guid.NewGuid();
            currentUser.IsAuthenticated = true;
            currentUser.AdminId = adminId;
            adminAccountReader.NextAdmin = CreateAdministrator(adminId);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await handler.Handle(new GetCurrentAdminQuery(), cancellationToken);

            Assert.Equal(cancellationToken, adminAccountReader.LastCancellationToken);
        }

        [Fact]
        public async Task AnUnauthenticatedCurrentUser_ReturnsTheSafeUnauthorizedError_WithoutReadingTheAccount()
        {
            currentUser.IsAuthenticated = false;
            currentUser.AdminId = Guid.NewGuid();

            var result = await handler.Handle(new GetCurrentAdminQuery(), CancellationToken.None);

            AssertUnavailable(result);
            Assert.Empty(adminAccountReader.RequestedAdminIds);
        }

        [Fact]
        public async Task ACurrentUserWithoutAnAdministratorId_ReturnsTheSafeUnauthorizedError_WithoutReadingTheAccount()
        {
            currentUser.IsAuthenticated = true;
            currentUser.AdminId = null;

            var result = await handler.Handle(new GetCurrentAdminQuery(), CancellationToken.None);

            AssertUnavailable(result);
            Assert.Empty(adminAccountReader.RequestedAdminIds);
        }

        [Fact]
        public async Task AnEmptyAdministratorId_ReturnsTheSafeUnauthorizedError_WithoutReadingTheAccount()
        {
            currentUser.IsAuthenticated = true;
            currentUser.AdminId = Guid.Empty;

            var result = await handler.Handle(new GetCurrentAdminQuery(), CancellationToken.None);

            AssertUnavailable(result);
            Assert.Empty(adminAccountReader.RequestedAdminIds);
        }

        [Fact]
        public async Task AnAdministratorThatIsMissingOrInactive_ReturnsTheSameSafeUnauthorizedError()
        {
            var adminId = Guid.NewGuid();
            currentUser.IsAuthenticated = true;
            currentUser.AdminId = adminId;
            adminAccountReader.NextAdmin = null;

            var result = await handler.Handle(new GetCurrentAdminQuery(), CancellationToken.None);

            AssertUnavailable(result);
            Assert.Equal(new[] { adminId }, adminAccountReader.RequestedAdminIds);
            Assert.DoesNotContain(AdminEmail, result.Error!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(adminId.ToString(), result.Error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ANullQuery_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => handler.Handle(null!, CancellationToken.None));
            Assert.Empty(adminAccountReader.RequestedAdminIds);
        }

        [Fact]
        public void TheHandler_RequiresBothDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new GetCurrentAdminQueryHandler(null!, adminAccountReader));
            Assert.Throws<ArgumentNullException>(() => new GetCurrentAdminQueryHandler(currentUser, null!));
        }
    }
}
