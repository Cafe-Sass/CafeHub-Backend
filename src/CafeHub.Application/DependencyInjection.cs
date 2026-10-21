using CafeHub.Application.Abstractions.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var applicationAssembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(applicationAssembly);
                AddPipelineBehaviors(services, configuration);
            });
            services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

            return services;
        }

        /// <summary>
        /// Registers every request pipeline behavior in the fixed order: Logging, then Validation, then Transaction, then the handler.
        /// In containers without logging infrastructure, the logger falls back to a no-op implementation so requests still dispatch.
        /// </summary>
        private static void AddPipelineBehaviors(IServiceCollection services, MediatRServiceConfiguration configuration)
        {
            services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        }
    }
}
