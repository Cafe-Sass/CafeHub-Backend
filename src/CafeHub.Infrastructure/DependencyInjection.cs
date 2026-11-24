using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Notifications;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tenancy;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Contracts;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Authentication;
using CafeHub.Infrastructure.Billing;
using CafeHub.Infrastructure.Branches;
using CafeHub.Infrastructure.CafeProfile;
using CafeHub.Infrastructure.Guests;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Menu;
using CafeHub.Infrastructure.Notifications;
using CafeHub.Infrastructure.Onboarding;
using CafeHub.Infrastructure.Onboarding.Payments;
using CafeHub.Infrastructure.Orders;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using CafeHub.Infrastructure.Persistence.Master.Onboarding;
using CafeHub.Infrastructure.Persistence.Master.Plans;
using CafeHub.Infrastructure.Persistence.Master.Provisioning;
using CafeHub.Infrastructure.Persistence.Master.Subscriptions;
using CafeHub.Infrastructure.Persistence.Master.Upgrades;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Staff;
using CafeHub.Infrastructure.Stations;
using CafeHub.Infrastructure.Tables;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.Infrastructure.Upgrades;
using CafeHub.Infrastructure.Wifi;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
            services.AddScoped<IMasterDatabaseProbe, MasterDatabaseProbe>();

            services.AddSingleton(typeof(ISecretReferenceResolver), typeof(ConfigurationSecretReferenceResolver));
            services.AddScoped<ITenantRegistryReader, TenantRegistryReader>();
            services.AddScoped<ITenantResolver, TenantResolver>();
            services.AddScoped<ITenantStateGuard, TenantStateGuard>();
            services.AddScoped(typeof(ITenantContextAccessor), typeof(TenantContextAccessor));
            services.AddScoped(serviceProvider => (ITenantContextSetter)serviceProvider.GetRequiredService<ITenantContextAccessor>());
            services.AddSingleton(_ => TenantDatabaseOptionsBinder.Bind(configuration));
            services.AddSingleton(serviceProvider =>
                new Lazy<TenantDatabaseOptions>(() => serviceProvider.GetRequiredService<TenantDatabaseOptions>()));
            services.AddScoped<ITenantConnectionResolver, RegistryTenantConnectionResolver>();
            services.AddDbContext<TenantDbContext>((serviceProvider, options) =>
            {
                var tenantConnectionResolver = serviceProvider.GetRequiredService<ITenantConnectionResolver>();
                options.UseNpgsql(tenantConnectionResolver.Resolve());
            });
            services.AddScoped<IMasterUnitOfWork, MasterUnitOfWork>();
            services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();

            services.AddSingleton(_ => JwtOptionsBinder.Bind(configuration));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

            services.AddSingleton(_ => RefreshTokenOptionsBinder.Bind(configuration));
            services.AddSingleton<ITokenHasher, TokenHasher>();
            services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
            services.AddSingleton<ITableQrCodeGenerator, TableQrCodeGenerator>();
            services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();

            var identityLockoutOptions = IdentityOptionsBinder.Bind(configuration);
            services.AddSingleton(identityLockoutOptions);
            services.AddSingleton(_ => PlatformSeedOptionsBinder.Bind(configuration));
            services.Configure<IdentityOptions>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = identityLockoutOptions.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityLockoutOptions.LockoutMinutes);
            });

            services.AddIdentityCore<PlatformAdminUser>()
                .AddRoles<PlatformAdminRole>();
            services.RemoveAll<IPasswordValidator<PlatformAdminUser>>();
            services.AddScoped<IPasswordValidator<PlatformAdminUser>, PlatformAdminPasswordPolicy<PlatformAdminUser>>();
            services.AddScoped<IUserStore<PlatformAdminUser>, PlatformAdminUserStore>();
            services.AddScoped<IRoleStore<PlatformAdminRole>>(provider =>
                new RoleStore<
                    PlatformAdminRole,
                    MasterDbContext,
                    Guid,
                    PlatformAdminUserRole,
                    PlatformAdminRoleClaim>(
                        provider.GetRequiredService<MasterDbContext>()));
            services.Replace(ServiceDescriptor.Scoped<ILookupNormalizer, PlatformAdminLookupNormalizer>());
            services.AddScoped<IAdminAuthenticator, AdminAuthenticator>();
            services.AddIdentityCore<CafeHub.Domain.Tenants.TenantUser>()
                .AddRoles<CafeHub.Domain.Tenants.TenantRole>()
                .AddUserStore<TenantUserStore>()
                .AddRoleStore<TenantRoleStore>();
            services.RemoveAll<IPasswordValidator<CafeHub.Domain.Tenants.TenantUser>>();
            services.AddScoped<IPasswordValidator<CafeHub.Domain.Tenants.TenantUser>, PlatformAdminPasswordPolicy<CafeHub.Domain.Tenants.TenantUser>>();
            services.AddScoped<ITenantUserAuthenticator, TenantUserAuthenticator>();
            services.AddScoped<TenantAccountReader>();
            services.AddScoped<ITenantAccountReader>(provider => provider.GetRequiredService<TenantAccountReader>());
            services.AddScoped<ITenantPermissionReader, TenantPermissionReader>();
            services.AddScoped<CafeHub.Application.Roles.ITenantRoleAdministration, TenantRoleStore>();
            services.AddSingleton(serviceProvider => TenantJwtOptionsBinder.Bind(configuration, serviceProvider.GetRequiredService<JwtOptions>().Audience));
            services.AddSingleton<ITenantAccessTokenIssuer, TenantJwtAccessTokenIssuer>();
            services.AddScoped<ITenantRefreshTokenStore, TenantRefreshTokenStore>();
            services.AddScoped<ICafeSettingsReader, CafeSettingsStore>();
            services.AddScoped<ICafeSettingsWriter, CafeSettingsWriter>();
            services.AddScoped<ICafePaymentSettingsReader, CafePaymentSettingsStore>();
            services.AddScoped<ICafePaymentSettingsWriter, CafePaymentSettingsStore>();
            services.AddScoped<IBranchStore, BranchStore>();
            services.AddScoped<IStationStore, StationStore>();
            services.AddScoped<IMenuCategoryStore, MenuCategoryStore>();
            services.AddScoped<IProductStore, ProductStore>();
            services.AddScoped<IProductCatalogSchema, ProductCatalogSchema>();
            services.AddScoped<IProductStationAssignmentStore, ProductStationAssignmentStore>();
            services.AddScoped<IBranchProductAvailabilityStore, BranchProductAvailabilityStore>();
            services.AddScoped<IGuestMenuStore, GuestMenuStore>();
            services.AddScoped<IOrderNumberAllocator, OrderNumberAllocator>();
            services.AddScoped<IOrderStore, OrderStore>();
            services.AddScoped<IOrderTableStore, OrderTableStore>();
            services.AddScoped<IOrderFeedStore, OrderFeedStore>();
            services.AddScoped<IGuestOrderStore, GuestOrderStore>();
            services.AddScoped<IOrderDecisionStore, OrderDecisionStore>();
            services.AddScoped<IOrderExpiryStore, OrderExpiryStore>();
            services.AddScoped<IMissedResponseStore, MissedResponseStore>();
            services.AddScoped<IGuestMuteStore, GuestMuteStore>();
            services.AddScoped<IOrderPublicReferenceGenerator, OrderPublicReferenceGenerator>();
            services.AddScoped<IBillStore, BillStore>();
            services.AddScoped<IBillQueryStore, BillQueryStore>();
            services.AddScoped<IBillPublicReferenceGenerator, BillPublicReferenceGenerator>();
            services.AddScoped<IPaymentStore, PaymentStore>();
            services.AddScoped<IPaymentQueryStore, PaymentQueryStore>();
            services.AddScoped<IReceiptIssuer, ReceiptStore>();
            services.AddScoped<IReceiptQueryStore, ReceiptQueryStore>();
            services.AddScoped<IWifiSettingsStore, WifiSettingsStore>();
            services.AddScoped<IOrderNumberCounterStore, OrderNumberCounterStore>();
            services.AddScoped<ICafeBusinessDateProvider, CafeBusinessDateProvider>();
            services.AddScoped<IStationScope, StationScope>();
            services.AddScoped<ITableStore, TableStore>();
            services.AddScoped<IStaffStore, StaffStore>();
            services.AddScoped<ITenantRefreshTokenService, TenantRefreshTokenService>();
            services.AddScoped<IAdminAccountReader, AdminAccountReader>();
            services.AddScoped<ISuperAdminSeeder, SuperAdminSeeder>();

            services.AddScoped<IPlatformAuditRecordStore, PlatformAuditRecordStore>();
            services.AddScoped<IPlatformAuditWriter, PlatformAuditWriter>();
            services.AddScoped<ITenantAuditWriter, TenantAuditWriter>();
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
            services.AddSingleton(serviceProvider =>
                new Lazy<TenantDatabaseCreatorOptions>(() => serviceProvider.GetRequiredService<TenantDatabaseCreatorOptions>()));
            services.AddSingleton<ITenantCredentialDeriver, HmacTenantCredentialDeriver>();
            services.AddSingleton<ITenantProvisioningSqlExecutor>(provider =>
                new NpgsqlProvisioningSqlExecutor(provider.GetRequiredService<TenantDatabaseCreatorOptions>()));
            services.AddScoped<ITenantDatabaseCreator, TenantDatabaseCreator>();
            services.AddSingleton(_ => TenantSchemaInitializerOptionsBinder.Bind(configuration));
            services.AddScoped<ITenantSchemaInitializer, TenantSchemaInitializer>();
            services.AddSingleton<ITenantMigrationApplicator, NpgsqlTenantMigrationApplicator>();
            services.AddScoped<ITenantSeeder, TenantSeeder>();
            services.AddScoped<ITenantSchemaUpgradeRunStore, TenantSchemaUpgradeRunStore>();
            services.AddScoped<ITenantSchemaUpgradeRunClaimer, TenantSchemaUpgradeRunClaimer>();
            services.AddScoped<TenantSchemaUpgradeService>();
            services.AddScoped<ITenantSchemaUpgradeRunner>(provider =>
                provider.GetRequiredService<TenantSchemaUpgradeService>());
            services.AddSingleton<ITenantSchemaUpgrader, NpgsqlTenantSchemaUpgrader>();
            services.AddSingleton(_ => TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration));
            services.AddHostedService<TenantSchemaUpgradeWorker>();
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
