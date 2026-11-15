using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Identity.Queries;
using CafeHub.Application.Tenancy;

namespace CafeHub.UnitTests
{
    public sealed class GetCurrentTenantUserQueryHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private readonly FakeCurrentTenantUser currentTenantUser = new();
        private readonly StubTenantUserAuthenticator tenantUserAuthenticator = new();
        private readonly FakeTenantPermissionReader permissionReader = new();
        private readonly StubTenantContextAccessor tenantContextAccessor = new(TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));
        private readonly GetCurrentTenantUserQueryHandler handler;

        public GetCurrentTenantUserQueryHandlerTests()
        {
            handler = new GetCurrentTenantUserQueryHandler(currentTenantUser, tenantUserAuthenticator, tenantContextAccessor, permissionReader);
        }

        private static AuthenticatedTenantUser CreateUser(Guid userId)
        {
            return new AuthenticatedTenantUser
            {
                Id = userId,
                Email = "owner@example.com",
                FullName = "Cafe Owner",
                UserKind = "Owner",
                Roles = new[] { "Owner", "Manager" },
            };
        }

        private static void AssertUnavailable(Result<TenantMeResponse> result)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableCode, result.Error!.Code);
            Assert.Equal(AuthErrors.CurrentTenantUserUnavailableMessage, result.Error.Message);
            Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        }

        [Fact]
        public async Task AnAuthenticatedActiveTenantUser_IsReturnedWithFreshStoredRolesAndTenantSlug()
        {
            currentTenantUser.IsAuthenticated = true;
            currentTenantUser.UserId = UserId;
            currentTenantUser.TenantId = TenantId;
            currentTenantUser.Roles = new[] { "StaleTokenRole" };
            tenantUserAuthenticator.NextUser = CreateUser(UserId);

            var result = await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(UserId, result.Value.UserId);
            Assert.Equal("owner@example.com", result.Value.Email);
            Assert.Equal("Cafe Owner", result.Value.FullName);
            Assert.Equal("Owner", result.Value.UserKind);
            Assert.Equal(new[] { "Owner", "Manager" }, result.Value.Roles);
            Assert.Equal("cafe-one", result.Value.TenantSlug);
            Assert.Equal(new[] { UserId }, tenantUserAuthenticator.RequestedUserIds);
        }

        [Fact]
        public async Task TheQuery_ForwardsTheCancellationToken_ToTheTenantUserReader()
        {
            currentTenantUser.IsAuthenticated = true;
            currentTenantUser.UserId = UserId;
            currentTenantUser.TenantId = TenantId;
            tenantUserAuthenticator.NextUser = CreateUser(UserId);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await handler.Handle(new GetCurrentTenantUserQuery(), cancellationToken);

            Assert.Equal(cancellationToken, tenantUserAuthenticator.LastCancellationToken);
        }

        [Fact]
        public async Task AnUnauthenticatedOrMissingIdCurrentUser_ReturnsUnauthorizedWithoutReadingTheUser()
        {
            currentTenantUser.IsAuthenticated = false;
            currentTenantUser.UserId = UserId;
            currentTenantUser.TenantId = TenantId;

            AssertUnavailable(await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None));

            currentTenantUser.IsAuthenticated = true;
            currentTenantUser.UserId = null;
            AssertUnavailable(await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None));

            currentTenantUser.UserId = Guid.Empty;
            AssertUnavailable(await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None));
            Assert.Empty(tenantUserAuthenticator.RequestedUserIds);
        }

        [Fact]
        public async Task APlatformOrCrossTenantPrincipal_ReturnsUnauthorizedWithoutReadingTheUser()
        {
            currentTenantUser.IsAuthenticated = true;
            currentTenantUser.UserId = UserId;
            currentTenantUser.TenantId = null;

            AssertUnavailable(await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None));

            currentTenantUser.TenantId = Guid.NewGuid();
            AssertUnavailable(await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None));
            Assert.Empty(tenantUserAuthenticator.RequestedUserIds);
        }

        [Fact]
        public async Task ADeletedOrMissingTenantUser_ReturnsTheSameGenericUnauthorizedError()
        {
            currentTenantUser.IsAuthenticated = true;
            currentTenantUser.UserId = UserId;
            currentTenantUser.TenantId = TenantId;
            tenantUserAuthenticator.NextUser = null;

            var result = await handler.Handle(new GetCurrentTenantUserQuery(), CancellationToken.None);

            AssertUnavailable(result);
            Assert.DoesNotContain("owner@example.com", result.Error!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(UserId.ToString(), result.Error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task MeReturnsCurrentBranchAndDatabasePermissions()
        {
            currentTenantUser.IsAuthenticated = true;
            currentTenantUser.UserId = UserId;
            currentTenantUser.TenantId = TenantId;
            currentTenantUser.BranchId = Guid.NewGuid();
            tenantUserAuthenticator.NextUser = new AuthenticatedTenantUser
            {
                Id = UserId,
                Email = "staff@example.test",
                FullName = "Staff",
                UserKind = "Staff",
                Roles = ["Manager"],
            };
            permissionReader.Codes = [TenantPermissions.StaffRead];

            var result = await handler.Handle(new GetCurrentTenantUserQuery(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(currentTenantUser.BranchId, result.Value.BranchId);
            Assert.Equal([TenantPermissions.StaffRead], result.Value.EffectivePermissions);
            Assert.Equal(1, permissionReader.Reads);
            Assert.Equal(currentTenantUser.BranchId, permissionReader.ExpectedBranchId);
        }

        [Fact]
        public void TenantMeResponse_ExposesExactlyEightSafeMembers()
        {
            var properties = typeof(TenantMeResponse).GetProperties();

            Assert.Equal(8, properties.Length);
            Assert.Equal(
                new[] { "UserId", "Email", "FullName", "UserKind", "Roles", "TenantSlug", "BranchId", "EffectivePermissions" }
                    .OrderBy(name => name, StringComparer.Ordinal),
                properties.Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
            Assert.DoesNotContain(properties, property => property.Name.Contains("hash", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("stamp", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("token", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ANullQuery_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
            Assert.Empty(tenantUserAuthenticator.RequestedUserIds);
        }

        [Fact]
        public void TheHandler_RequiresAllDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new GetCurrentTenantUserQueryHandler(null!, tenantUserAuthenticator, tenantContextAccessor, new FakeTenantPermissionReader()));
            Assert.Throws<ArgumentNullException>(() => new GetCurrentTenantUserQueryHandler(currentTenantUser, null!, tenantContextAccessor, new FakeTenantPermissionReader()));
            Assert.Throws<ArgumentNullException>(() => new GetCurrentTenantUserQueryHandler(currentTenantUser, tenantUserAuthenticator, null!, new FakeTenantPermissionReader()));
        }

        private sealed class FakeCurrentTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; set; }

            public Guid? UserId { get; set; }

            public Guid? TenantId { get; set; }

            public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

            public Guid? BranchId { get; set; }
        }

        private sealed class StubTenantUserAuthenticator : ITenantUserAuthenticator
        {
            public AuthenticatedTenantUser? NextUser { get; set; }

            public List<Guid> RequestedUserIds { get; } = [];

            public CancellationToken LastCancellationToken { get; private set; }

            public Task<Result<AuthenticatedTenantUser>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<AuthenticatedTenantUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
            {
                RequestedUserIds.Add(userId);
                LastCancellationToken = cancellationToken;
                return Task.FromResult(NextUser);
            }
        }

        private sealed class StubTenantContextAccessor(TenantContext tenantContext) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => tenantContext;
        }
    }
}
