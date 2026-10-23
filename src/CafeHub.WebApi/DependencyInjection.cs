using CafeHub.Application.Abstractions.Correlation;
using CafeHub.WebApi.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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

        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder applicationBuilder)
        {
            if (applicationBuilder is null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            return applicationBuilder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}
