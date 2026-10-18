using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<MasterConnectionStringResolver>();
        services.AddDbContext<MasterDbContext>((serviceProvider, options) =>
        {
            var connectionStringResolver = serviceProvider.GetRequiredService<MasterConnectionStringResolver>();
            options.UseNpgsql(connectionStringResolver.Resolve());
        });

        return services;
    }
}
