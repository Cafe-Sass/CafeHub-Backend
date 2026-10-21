using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var applicationAssembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(applicationAssembly));
            services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

            return services;
        }
    }
}
