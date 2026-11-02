using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.WebApi.Security
{
    public static class SecurityExtensions
    {
        public static IServiceCollection AddRequestLimits(this IServiceCollection services, IConfiguration configuration)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var options = RequestLimitsOptionsBinder.Bind(configuration);
            services.AddSingleton(options);

            services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(kestrelOptions =>
            {
                kestrelOptions.Limits.MaxRequestHeadersTotalSize = options.MaxHeadersTotalBytes;
            });

            return services;
        }

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder applicationBuilder)
        {
            if (applicationBuilder is null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            return applicationBuilder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
