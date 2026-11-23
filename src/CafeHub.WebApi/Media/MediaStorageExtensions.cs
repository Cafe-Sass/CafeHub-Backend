using CafeHub.Application.Media.Contracts;
using CafeHub.Infrastructure.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CafeHub.WebApi.Media
{
    public static class MediaStorageExtensions
    {
        public static IServiceCollection AddMediaStorage(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            var options = MediaOptionsBinder.Bind(configuration, environment.ContentRootPath);
            services.AddSingleton(options);
            services.AddScoped<IMediaStorage, LocalDiskMediaStorage>();

            return services;
        }
    }
}
