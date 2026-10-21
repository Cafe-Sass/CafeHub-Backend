using CafeHub.Application.Abstractions.Behaviors;
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

            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(applicationAssembly);
                AddPipelineBehaviors(configuration);
            });
            services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

            return services;
        }

        /// <summary>
        /// Registers every request pipeline behavior in the fixed order: Logging, then Validation, then Transaction, then the handler.
        /// </summary>
        private static void AddPipelineBehaviors(MediatRServiceConfiguration configuration)
        {
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        }
    }
}
