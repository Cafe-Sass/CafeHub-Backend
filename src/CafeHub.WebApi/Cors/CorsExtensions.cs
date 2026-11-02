using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.WebApi.Cors
{
    public static class CorsExtensions
    {
        public const string PolicyName = "CafeHubCors";

        public static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration configuration)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var options = CorsOptionsBinder.Bind(configuration);
            services.AddSingleton(options);

            services.AddCors(corsOptions =>
            {
                corsOptions.AddPolicy(
                    PolicyName,
                    builder =>
                    {
                        if (options.AllowedOrigins.Count > 0)
                        {
                            builder.WithOrigins(options.AllowedOrigins.ToArray());
                        }

                        builder.WithMethods(options.AllowedMethods.ToArray());
                        builder.WithHeaders(options.AllowedHeaders.ToArray());
                        builder.WithExposedHeaders(options.ExposedHeaders.ToArray());
                        if (options.AllowCredentials)
                        {
                            builder.AllowCredentials();
                        }
                        builder.SetPreflightMaxAge(TimeSpan.FromSeconds(options.MaxAgeSeconds));
                    });
            });

            return services;
        }

        public static IApplicationBuilder UseConfiguredCors(this IApplicationBuilder applicationBuilder)
        {
            if (applicationBuilder is null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            return applicationBuilder.UseCors(PolicyName);
        }
    }
}
