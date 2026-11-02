using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.WebApi.Transport
{
    public static class TransportSecurityExtensions
    {
        public static IServiceCollection AddTransportSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var options = TransportSecurityOptionsBinder.Bind(configuration);
            services.AddSingleton(options);
            services.AddHsts(hstsOptions =>
            {
                hstsOptions.MaxAge = options.HstsMaxAge;
                hstsOptions.IncludeSubDomains = options.Hsts.IncludeSubDomains;
                hstsOptions.Preload = options.Hsts.Preload;
            });
            services.AddHttpsRedirection(redirectionOptions =>
            {
                if (options.HttpsPort is not null)
                {
                    redirectionOptions.HttpsPort = options.HttpsPort;
                }
            });
            services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
            {
                forwardedHeadersOptions.ForwardedHeaders = options.ForwardedHeaders.Enabled
                    ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                    : ForwardedHeaders.None;
                forwardedHeadersOptions.ForwardLimit = options.ForwardedHeaders.ForwardLimit;
                forwardedHeadersOptions.KnownProxies.Clear();
                forwardedHeadersOptions.KnownIPNetworks.Clear();

                foreach (var proxy in options.ForwardedHeaders.KnownProxies)
                {
                    forwardedHeadersOptions.KnownProxies.Add(proxy);
                }

                foreach (var network in options.ForwardedHeaders.KnownNetworks)
                {
                    forwardedHeadersOptions.KnownIPNetworks.Add(network);
                }
            });

            return services;
        }

        public static IApplicationBuilder UseTransportSecurity(this IApplicationBuilder applicationBuilder, IHostEnvironment environment)
        {
            if (applicationBuilder is null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (!TransportSecurityPolicy.ShouldApplyHttps(environment))
            {
                return applicationBuilder;
            }

            applicationBuilder.UseHsts();
            applicationBuilder.UseHttpsRedirection();

            return applicationBuilder;
        }
    }
}
