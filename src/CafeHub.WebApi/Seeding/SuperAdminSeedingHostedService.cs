using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Seeding
{
    internal sealed class SuperAdminSeedingHostedService : IHostedService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly PlatformSeedOptions options;
        private readonly ILogger<SuperAdminSeedingHostedService> logger;

        public SuperAdminSeedingHostedService(
            IServiceScopeFactory scopeFactory,
            PlatformSeedOptions options,
            ILogger<SuperAdminSeedingHostedService> logger)
        {
            this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!options.IsConfigured)
            {
                logger.LogInformation(
                    "Super admin seeding skipped because {ConfigurationPath} is not set.",
                    PlatformSeedConfigurationKeys.SuperAdminEmailPath);

                return;
            }

            using var scope = scopeFactory.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<ISuperAdminSeeder>();
            var result = await seeder.SeedAsync(cancellationToken);

            if (result.IsFailure)
            {
                throw new InvalidOperationException(result.Error!.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
