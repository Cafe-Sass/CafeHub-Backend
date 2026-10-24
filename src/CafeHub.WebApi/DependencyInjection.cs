using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Infrastructure.Authentication;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Correlation;
using CafeHub.WebApi.Documentation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            services.AddAuthorization();

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
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer(new ProblemDetailsDocumentTransformer());
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
                endpointRouteBuilder.MapOpenApi();
            }
        }
    }
}
