using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.Infrastructure
{
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

            services.AddSingleton(typeof(ISecretReferenceResolver), typeof(ConfigurationSecretReferenceResolver));
            services.AddScoped(typeof(ITenantContextAccessor), typeof(TenantContextAccessor));
            services.AddScoped(serviceProvider => (ITenantContextSetter)serviceProvider.GetRequiredService<ITenantContextAccessor>());
            services.AddScoped(typeof(ITenantConnectionResolver), typeof(TenantConnectionResolver));
            services.AddDbContext<TenantDbContext>((serviceProvider, options) =>
            {
                var tenantContextAccessor = serviceProvider.GetRequiredService<ITenantContextAccessor>();
                var tenantConnectionResolver = serviceProvider.GetRequiredService<ITenantConnectionResolver>();
                options.UseNpgsql(tenantConnectionResolver.Resolve(tenantContextAccessor.GetRequired()));
            });

            return services;
        }
    }
}
