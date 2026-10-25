using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Authentication;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            services.AddScoped<IMasterUnitOfWork, MasterUnitOfWork>();
            services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();

            services.AddSingleton(_ => JwtOptionsBinder.Bind(configuration));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

            services.AddSingleton(_ => RefreshTokenOptionsBinder.Bind(configuration));
            services.AddSingleton<ITokenHasher, TokenHasher>();
            services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
            services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();

            var identityLockoutOptions = IdentityOptionsBinder.Bind(configuration);
            services.AddSingleton(identityLockoutOptions);
            services.Configure<IdentityOptions>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = identityLockoutOptions.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityLockoutOptions.LockoutMinutes);
            });

            services.AddIdentityCore<PlatformAdminUser>()
                .AddRoles<PlatformAdminRole>()
                .AddEntityFrameworkStores<MasterDbContext>();
            services.Replace(ServiceDescriptor.Scoped<ILookupNormalizer, PlatformAdminLookupNormalizer>());
            services.AddScoped<IAdminAuthenticator, AdminAuthenticator>();

            services.AddScoped<IPlatformAuditRecordStore, PlatformAuditRecordStore>();
            services.AddScoped<IPlatformAuditWriter, PlatformAuditWriter>();

            return services;
        }
    }
}
