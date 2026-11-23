using System.Reflection;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Billing
{
    public sealed class TenantBillsControllerTests
    {
        private const string Route = "t/{slug}/api/v1/bills";

        [Fact]
        public void List_IsAnAuthorizedGetActionOnTheBillsRoute()
        {
            var action = typeof(TenantBillsController).GetMethod(nameof(TenantBillsController.List))!;

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.BillsView, authorize.Policy);
            Assert.Equal(Route, typeof(TenantBillsController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void GetById_IsAnAuthorizedGetActionOnTheBillRoute()
        {
            var action = typeof(TenantBillsController).GetMethod(nameof(TenantBillsController.GetById))!;

            var route = Assert.IsType<HttpGetAttribute>(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Equal("{billId:guid}", route.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.BillsView, authorize.Policy);
            Assert.NotNull(action.GetParameters().Single(parameter => parameter.Name == "billId"));
        }

        [Fact]
        public void ConfirmCash_IsAnAuthorizedPostActionWithTheIdempotencyHeader()
        {
            var action = typeof(TenantBillsController).GetMethod(nameof(TenantBillsController.ConfirmCash))!;

            var route = Assert.IsType<HttpPostAttribute>(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal("{billId:guid}/payments/cash", route.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.PaymentsCashConfirm, authorize.Policy);
            var header = action.GetParameters()
                .Single(parameter => parameter.Name == "idempotencyKey")
                .GetCustomAttribute<FromHeaderAttribute>();
            Assert.Equal("Idempotency-Key", header!.Name);
        }

        [Fact]
        public void Close_IsAnAuthorizedPostActionOnTheBillRoute()
        {
            var action = typeof(TenantBillsController).GetMethod(nameof(TenantBillsController.Close))!;

            var route = Assert.IsType<HttpPostAttribute>(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal("{billId:guid}/close", route.Template);
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.BillsClose, authorize.Policy);
            Assert.NotNull(action.GetParameters().Single(parameter => parameter.Name == "request"));
        }

        [Fact]
        public void TheBillPermissionsJoinTheCatalogAndStayGrantableToStaff()
        {
            Assert.Equal("bills.view", TenantPermissions.BillsView);
            Assert.Equal("bills.close", TenantPermissions.BillsClose);
            Assert.Contains(TenantPermissions.BillsView, TenantPermissions.All);
            Assert.Contains(TenantPermissions.BillsClose, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.BillsView));
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.BillsClose));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.BillsView));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.BillsClose));
        }
    }
}
