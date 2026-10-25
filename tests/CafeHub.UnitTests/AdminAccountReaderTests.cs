using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests
{
    public sealed class AdminAccountReaderTests
    {
        private const string AdminEmail = "active.admin@cafehub.app";

        private static UserManager<PlatformAdminUser> CreateUserManager(FakePlatformAdminUserStore store)
        {
            return new UserManager<PlatformAdminUser>(
                store,
                Options.Create(new IdentityOptions()),
                new RecordingPasswordHasher(),
                Array.Empty<IUserValidator<PlatformAdminUser>>(),
                Array.Empty<IPasswordValidator<PlatformAdminUser>>(),
                new PlatformAdminLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<PlatformAdminUser>>.Instance);
        }

        private static PlatformAdminUser CreateAdminUser(string email)
        {
            return new PlatformAdminUser(email)
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = PlatformAdminEmail.Normalize(email),
                SecurityStamp = Guid.NewGuid().ToString("N"),
            };
        }

        [Fact]
        public async Task AnActiveAdmin_IsReturnedWithTheStoredRoles()
        {
            var user = CreateAdminUser(AdminEmail);
            var store = new FakePlatformAdminUserStore(user);
            store.AssignRole(user.Id, PlatformAdminRoles.SuperAdmin);
            var reader = new AdminAccountReader(CreateUserManager(store));

            var result = await reader.FindActiveByIdAsync(user.Id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(user.Id, result.Id);
            Assert.Equal(AdminEmail, result.Email);
            Assert.Equal(new[] { PlatformAdminRoles.SuperAdmin }, result.Roles);
        }

        [Fact]
        public async Task AnUnknownAdminId_ReturnsNull()
        {
            var reader = new AdminAccountReader(CreateUserManager(new FakePlatformAdminUserStore()));

            var result = await reader.FindActiveByIdAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task AnInactiveAdmin_ReturnsNull()
        {
            var user = CreateAdminUser(AdminEmail);
            user.IsActive = false;
            var store = new FakePlatformAdminUserStore(user);
            var reader = new AdminAccountReader(CreateUserManager(store));

            var result = await reader.FindActiveByIdAsync(user.Id, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task AnEmptyAdminId_Throws()
        {
            var reader = new AdminAccountReader(CreateUserManager(new FakePlatformAdminUserStore()));

            await Assert.ThrowsAsync<ArgumentException>(() => reader.FindActiveByIdAsync(Guid.Empty, CancellationToken.None));
        }
    }
}
