using CafeHub.Application;
using CafeHub.Application.Plans.Commands;
using CafeHub.Application.Plans.Queries;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Plans
{
    public sealed class PlansRegistrationTests
    {
        [Fact]
        public void AddApplication_RegistersEveryPlansValidator()
        {
            var services = new ServiceCollection();
            services.AddApplication();
            using var serviceProvider = services.BuildServiceProvider();

            Assert.NotNull(serviceProvider.GetService<IValidator<CreatePlanCommand>>());
            Assert.NotNull(serviceProvider.GetService<IValidator<CreatePlanVersionCommand>>());
            Assert.NotNull(serviceProvider.GetService<IValidator<GetPlanQuery>>());
            Assert.NotNull(serviceProvider.GetService<IValidator<ListPlansQuery>>());
        }
    }
}
