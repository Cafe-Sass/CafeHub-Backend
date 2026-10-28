using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Authentication;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Correlation;
using CafeHub.WebApi.Documentation;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace CafeHub.WebApi
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCorrelationId(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddHttpContextAccessor();
            services.AddSingleton<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
            services.AddSingleton<ICorrelationIdGenerator, GuidCorrelationIdGenerator>();

            return services;
        }

        public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var options = AuthRateLimitOptionsBinder.Bind(configuration);
            services.AddSingleton(options);
            services.AddRateLimiter(rateLimiterOptions =>
            {
                rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                rateLimiterOptions.OnRejected = AuthRateLimitRejectionWriter.WriteAsync;
                rateLimiterOptions.AddPolicy(
                    AuthRateLimiting.PolicyName,
                    httpContext => AuthRateLimitPolicy.CreatePartition(httpContext, options));
            });

            return services;
        }

        public static IServiceCollection AddSuperAdminSeeding(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddHostedService<SuperAdminSeedingHostedService>();

            return services;
        }

        public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<JwtOptions>((jwtBearerOptions, jwtOptions) =>
                {
                    jwtBearerOptions.MapInboundClaims = false;
                    jwtBearerOptions.TokenValidationParameters = PlatformTokenValidationParameters.Create(jwtOptions);
                    jwtBearerOptions.Events = new ProblemDetailsJwtBearerEvents();
                });

            return services;
        }

        public static IServiceCollection AddTenantAuthentication(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddAuthentication()
                .AddJwtBearer(TenantAuthenticationSchemes.Bearer);

            services.AddOptions<JwtBearerOptions>(TenantAuthenticationSchemes.Bearer)
                .Configure<JwtOptions, TenantJwtOptions>((jwtBearerOptions, jwtOptions, tenantJwtOptions) =>
                {
                    jwtBearerOptions.MapInboundClaims = false;
                    jwtBearerOptions.TokenValidationParameters = TenantTokenValidationParameters.Create(jwtOptions, tenantJwtOptions);
                    jwtBearerOptions.Events = new TenantJwtBearerEvents();
                });

            return services;
        }

        public static IServiceCollection AddPlatformAuthorization(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddAuthorizationBuilder()
                .AddPolicy(PlatformAuthorizationPolicies.PlatformAdmin, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(PlatformAdminRoles.SuperAdmin);
                })
                .AddPolicy(PlatformAuthorizationPolicies.SuperAdmin, policy =>
                {
                    policy.RequireRole(PlatformAdminRoles.SuperAdmin);
                });

            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

            return services;
        }

        public static IServiceCollection AddTenantAuthorization(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddAuthorizationBuilder()
                .AddPolicy(TenantPolicies.TenantUser, policy =>
                {
                    policy.AddAuthenticationSchemes(TenantAuthenticationSchemes.Bearer);
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new TenantBoundRequirement());
                })
                .AddPolicy(TenantPolicies.TenantOwner, policy =>
                {
                    policy.AddAuthenticationSchemes(TenantAuthenticationSchemes.Bearer);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(TenantRoles.Owner);
                    policy.AddRequirements(new TenantBoundRequirement());
                });

            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentTenantUser, HttpContextCurrentTenantUser>();
            services.AddScoped<IAuthorizationHandler, TenantBoundAuthorizationHandler>();

            return services;
        }

        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder applicationBuilder)
        {
            if (applicationBuilder is null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            return applicationBuilder.UseMiddleware<CorrelationIdMiddleware>();
        }

        public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<OpenApiExposurePolicy>();
            var bearerSecurityTransformer = new BearerSecurityTransformer();
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer(new ProblemDetailsDocumentTransformer());
                options.AddDocumentTransformer(bearerSecurityTransformer);
                options.AddOperationTransformer(bearerSecurityTransformer);
            });

            return services;
        }

        public static void MapApiDocumentation(this IEndpointRouteBuilder endpointRouteBuilder)
        {
            if (endpointRouteBuilder is null)
            {
                throw new ArgumentNullException(nameof(endpointRouteBuilder));
            }

            var exposurePolicy = endpointRouteBuilder.ServiceProvider.GetRequiredService<OpenApiExposurePolicy>();
            if (exposurePolicy.IsExposed())
            {
                endpointRouteBuilder.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
                endpointRouteBuilder.MapOpenApi();
                endpointRouteBuilder.MapSwaggerUI(
                    "swagger",
                    options => options.SwaggerEndpoint("/openapi/v1.json", "CafeHub API v1"));
            }
        }
    }
}
