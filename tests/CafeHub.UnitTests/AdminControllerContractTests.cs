using System.Reflection;
using CafeHub.Application;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Identity.Queries;
using CafeHub.Domain.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class AdminControllerContractTests
    {
        private static MethodInfo MeAction()
        {
            return typeof(AdminController).GetMethod(
                "Me",
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("The AdminController.Me action was not found.");
        }

        [Fact]
        public void AdminController_IsAnApiControllerOnTheDocumentedPlatformAdminRoute()
        {
            Assert.True(typeof(ControllerBase).IsAssignableFrom(typeof(AdminController)));
            Assert.NotNull(typeof(AdminController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(AdminController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("api/v1/admin", route!.Template);
        }

        [Fact]
        public void MeAction_IsGetMe_AndRequiresTheSuperAdminPolicy()
        {
            var action = MeAction();
            var httpGet = action.GetCustomAttribute<HttpGetAttribute>();
            Assert.NotNull(httpGet);
            Assert.Equal("me", httpGet!.Template);
            var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authorize);
            Assert.Equal(PlatformAuthorizationPolicies.SuperAdmin, authorize!.Policy);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(typeof(AdminController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void CurrentAdminResponse_ExposesExactlyTheDocumentedContract()
        {
            var properties = typeof(CurrentAdminResponse).GetProperties();

            Assert.Equal(3, properties.Length);
            Assert.Equal(typeof(Guid), properties.Single(property => property.Name == "Id").PropertyType);
            Assert.Equal(typeof(string), properties.Single(property => property.Name == "Email").PropertyType);
            Assert.Equal(
                typeof(IReadOnlyCollection<string>),
                properties.Single(property => property.Name == "Roles").PropertyType);
        }

        [Fact]
        public void GetCurrentAdminQuery_IsAQueryOfTheCurrentAdminResponse()
        {
            Assert.True(typeof(IQuery<CurrentAdminResponse>).IsAssignableFrom(typeof(GetCurrentAdminQuery)));
            Assert.True(
                typeof(IQueryHandler<GetCurrentAdminQuery, CurrentAdminResponse>)
                    .IsAssignableFrom(typeof(GetCurrentAdminQueryHandler)));
        }

        [Fact]
        public async Task GetCurrentAdminQuery_IsDiscoveredByMediatR_AndReturnsTheCurrentAdministrator()
        {
            var adminId = Guid.NewGuid();
            var currentUser = new FakeCurrentUser
            {
                IsAuthenticated = true,
                AdminId = adminId,
                Roles = new[] { PlatformAdminRoles.SuperAdmin },
            };
            var adminAccountReader = new StubAdminAccountReader
            {
                NextAdmin = new AuthenticatedAdmin
                {
                    Id = adminId,
                    Email = "current.admin@cafehub.app",
                    Roles = new[] { PlatformAdminRoles.SuperAdmin },
                },
            };
            var services = new ServiceCollection();
            services.AddApplication();
            services.AddSingleton<ICurrentUser>(currentUser);
            services.AddSingleton<IAdminAccountReader>(adminAccountReader);
            using var serviceProvider = services.BuildServiceProvider();
            var mediator = serviceProvider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GetCurrentAdminQuery());

            Assert.True(result.IsSuccess);
            Assert.Equal(adminId, result.Value.Id);
            Assert.Equal(new[] { adminId }, adminAccountReader.RequestedAdminIds);
        }
    }
}
