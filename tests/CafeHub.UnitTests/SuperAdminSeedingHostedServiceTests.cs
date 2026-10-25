using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Identity;
using CafeHub.WebApi;
using CafeHub.WebApi.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class SuperAdminSeedingHostedServiceTests
    {
        private const string SeedEmail = "seed.superadmin@cafehub.app";
        private const string SeedPassword = "Str0ng!SeedPassword";

        private static PlatformSeedOptions CreateConfiguredOptions()
        {
            return new PlatformSeedOptions
            {
                SuperAdminEmail = SeedEmail,
                SuperAdminPassword = SeedPassword,
            };
        }

        private static HostedServiceTestContext CreateContext(PlatformSeedOptions options, ISuperAdminSeeder? seeder)
        {
            return new HostedServiceTestContext(options, seeder);
        }

        [Fact]
        public async Task StartAsync_WhenTheEmailIsNotConfigured_SkipsWithoutResolvingTheSeeder()
        {
            var options = new PlatformSeedOptions
            {
                SuperAdminEmail = "   ",
                SuperAdminPassword = SeedPassword,
            };
            using var context = CreateContext(options, seeder: null);

            await context.Service.StartAsync(CancellationToken.None);

            var entry = Assert.Single(context.Logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains(PlatformSeedConfigurationKeys.SuperAdminEmailPath, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(SeedPassword, entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task StartAsync_WhenSeedingSucceeds_CompletesAndPassesTheCancellationToken()
        {
            var seeder = new StubSuperAdminSeeder(Result.Success(SeedOutcome.Created));
            using var context = CreateContext(CreateConfiguredOptions(), seeder);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await context.Service.StartAsync(cancellationToken);

            Assert.Equal(1, seeder.CallCount);
            Assert.Equal(cancellationToken, seeder.LastCancellationToken);
        }

        [Fact]
        public async Task StartAsync_WhenSeedingFails_ThrowsTheSafeMessageWithoutSecrets()
        {
            var seeder = new StubSuperAdminSeeder(Result.Failure<SeedOutcome>(PlatformSeedErrors.InvalidConfiguration()));
            using var context = CreateContext(CreateConfiguredOptions(), seeder);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Service.StartAsync(CancellationToken.None));

            Assert.Equal(PlatformSeedErrors.InvalidConfigurationMessage, exception.Message);
            Assert.DoesNotContain(SeedEmail, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(SeedPassword, exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, seeder.CallCount);
        }

        [Fact]
        public async Task StopAsync_Completes()
        {
            using var context = CreateContext(
                CreateConfiguredOptions(),
                new StubSuperAdminSeeder(Result.Success(SeedOutcome.Created)));

            await context.Service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public void AddSuperAdminSeeding_RegistersTheHostedService()
        {
            var services = new ServiceCollection();

            services.AddSuperAdminSeeding();

            var descriptor = Assert.Single(
                services,
                registration => registration.ServiceType == typeof(IHostedService));
            Assert.Equal(typeof(SuperAdminSeedingHostedService), descriptor.ImplementationType);
        }

        [Fact]
        public void AddSuperAdminSeeding_WithNullServices_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => CafeHub.WebApi.DependencyInjection.AddSuperAdminSeeding(null!));
        }

        private sealed class HostedServiceTestContext : IDisposable
        {
            private readonly ServiceProvider serviceProvider;

            public HostedServiceTestContext(PlatformSeedOptions options, ISuperAdminSeeder? seeder)
            {
                var services = new ServiceCollection();
                if (seeder is not null)
                {
                    services.AddSingleton<ISuperAdminSeeder>(seeder);
                }

                serviceProvider = services.BuildServiceProvider();
                Logger = new FakeLogger<SuperAdminSeedingHostedService>();
                Service = new SuperAdminSeedingHostedService(
                    serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                    options,
                    Logger);
            }

            public SuperAdminSeedingHostedService Service { get; }

            public FakeLogger<SuperAdminSeedingHostedService> Logger { get; }

            public void Dispose()
            {
                serviceProvider.Dispose();
            }
        }
    }

    internal sealed class StubSuperAdminSeeder : ISuperAdminSeeder
    {
        private readonly Result<SeedOutcome> result;

        public StubSuperAdminSeeder(Result<SeedOutcome> result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<Result<SeedOutcome>> SeedAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            LastCancellationToken = cancellationToken;

            return Task.FromResult(result);
        }
    }
}
