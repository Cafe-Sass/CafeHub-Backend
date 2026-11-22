using System.Reflection;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Orders
{
    public sealed class TenantOrdersControllerTests
    {
        [Fact]
        public void Feed_IsAnAuthorizedGetActionOnTheOrdersRoute()
        {
            var action = typeof(TenantOrdersController).GetMethod(nameof(TenantOrdersController.Feed))!;

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.OrdersView, authorize.Policy);
            Assert.Equal(
                "t/{slug}/api/v1/orders",
                typeof(TenantOrdersController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void GetById_IsAnAuthorizedGetActionOnTheOrderRoute()
        {
            var action = typeof(TenantOrdersController).GetMethod(nameof(TenantOrdersController.GetById))!;

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.OrdersView, authorize.Policy);
            Assert.Equal("orderId", action.GetParameters().Single(parameter => parameter.Name == "orderId").Name);
            Assert.Equal(
                "{orderId:guid}",
                action.GetCustomAttribute<HttpGetAttribute>()!.Template);
        }

        [Fact]
        public void TheOrdersViewPermissionIsGrantableToStaff()
        {
            Assert.Equal("orders.view", TenantPermissions.OrdersView);
            Assert.Contains(TenantPermissions.OrdersView, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.OrdersView));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.OrdersView));
        }

        [Fact]
        public void Accept_IsAnAuthorizedPostActionOnTheOrderRoute()
        {
            var action = typeof(TenantOrdersController).GetMethod(nameof(TenantOrdersController.Accept))!;

            Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.OrdersDecide, authorize.Policy);
            Assert.Equal("orderId", action.GetParameters().Single(parameter => parameter.Name == "orderId").Name);
            Assert.Equal(
                "{orderId:guid}/accept",
                action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        }

        [Fact]
        public void Reject_IsAnAuthorizedPostActionOnTheOrderRoute()
        {
            var action = typeof(TenantOrdersController).GetMethod(nameof(TenantOrdersController.Reject))!;

            Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.OrdersDecide, authorize.Policy);
            Assert.Equal("orderId", action.GetParameters().Single(parameter => parameter.Name == "orderId").Name);
            Assert.NotNull(action.GetParameters().Single(parameter => parameter.Name == "request"));
            Assert.Equal(
                "{orderId:guid}/reject",
                action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        }

        [Fact]
        public void TheOrdersDecidePermissionIsGrantableToStaff()
        {
            Assert.Equal("orders.decide", TenantPermissions.OrdersDecide);
            Assert.Contains(TenantPermissions.OrdersDecide, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.OrdersDecide));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.OrdersDecide));
        }
    }
}
