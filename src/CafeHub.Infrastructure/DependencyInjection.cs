using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Notifications;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Authentication;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Notifications;
using CafeHub.Infrastructure.Onboarding;
using CafeHub.Infrastructure.Onboarding.Payments;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using CafeHub.Infrastructure.Persistence.Master.Onboarding;
using CafeHub.Infrastructure.Persistence.Master.Plans;
using CafeHub.Infrastructure.Persistence.Master.Provisioning;
using CafeHub.Infrastructure.Persistence.Master.Subscriptions;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

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
            services.AddSingleton(_ => PlatformSeedOptionsBinder.Bind(configuration));
            services.Configure<IdentityOptions>(options =>
            {
                PlatformAdminPasswordPolicy.Apply(options);
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = identityLockoutOptions.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityLockoutOptions.LockoutMinutes);
            });

            services.AddIdentityCore<PlatformAdminUser>()
                .AddRoles<PlatformAdminRole>()
                .AddEntityFrameworkStores<MasterDbContext>();
            services.Replace(ServiceDescriptor.Scoped<ILookupNormalizer, PlatformAdminLookupNormalizer>());
            services.AddScoped<IAdminAuthenticator, AdminAuthenticator>();
            services.AddScoped<IAdminAccountReader, AdminAccountReader>();
            services.AddScoped<ISuperAdminSeeder, SuperAdminSeeder>();

            services.AddScoped<IPlatformAuditRecordStore, PlatformAuditRecordStore>();
            services.AddScoped<IPlatformAuditWriter, PlatformAuditWriter>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddScoped<IOnboardingRequestStore, OnboardingRequestStore>();
            services.AddScoped<IOnboardingStatusReader, OnboardingStatusReader>();
            services.AddScoped<ITenantStore, TenantStore>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<IProvisioningJobStore, ProvisioningJobStore>();
            services.AddScoped<IProvisioningJobClaimer, ProvisioningJobClaimer>();
            services.AddScoped<IProvisioningJobAttemptStore, ProvisioningJobAttemptStore>();
            services.AddScoped<ITenantDatabaseStore, TenantDatabaseStore>();
            services.AddScoped<IProvisioningJobRunner, ProvisioningOrchestrator>();
            services.AddSingleton(_ => ProvisioningWorkerOptionsBinder.Bind(configuration));
            services.AddHostedService<ProvisioningWorker>();
            services.AddSingleton(_ => TenantDatabaseCreatorOptionsBinder.Bind(configuration));
            services.AddSingleton<TenantCredentialDeriver>();
            services.AddSingleton<ITenantProvisioningSqlExecutor>(provider =>
                new NpgsqlProvisioningSqlExecutor(provider.GetRequiredService<TenantDatabaseCreatorOptions>()));
            services.AddScoped<ITenantDatabaseCreator, TenantDatabaseCreator>();
            services.AddSingleton(_ => TenantSchemaInitializerOptionsBinder.Bind(configuration));
            services.AddScoped<ITenantSchemaInitializer, TenantSchemaInitializer>();
            services.AddSingleton<ITenantMigrationApplicator, NpgsqlTenantMigrationApplicator>();
            services.AddScoped<ITenantSeeder, TenantSeeder>();
            services.AddSingleton(_ => ProvisioningRetryOptionsBinder.Bind(configuration));
            services.AddSingleton(_ => NotificationOptionsBinder.Bind(configuration));
            services.AddSingleton<INotificationSender, LoggingNotificationSender>();
            services.AddScoped<ISlugAvailability, SlugAvailability>();
            services.AddSingleton<IDuplicateRegistrationDetector, DuplicateRegistrationDetector>();
            services.AddSingleton<IOwnerPasswordHasher, OwnerPasswordHasher>();
            services.AddScoped<IPaymentTransactionStore, PaymentTransactionStore>();

            services.AddSingleton(sp =>
            {
                var hostingEnvironment = sp.GetRequiredService<IHostEnvironment>();
                var paymentOptions = PaymentOptionsBinder.Bind(configuration, hostingEnvironment.IsDevelopment());
                return paymentOptions.Mock;
            });
            services.AddSingleton<IPaymentGateway, MockPaymentGateway>();

            return services;
        }
    }
}
