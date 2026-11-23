using System.Reflection;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Billing
{
    public sealed class TenantPaymentSettingsControllerTests
    {
        private const string Route = "t/{slug}/api/v1/payment-settings";
        private const string GuestRoute = "t/{slug}/api/v1/guest/qr";

        [Fact]
        public void Get_IsAnAuthorizedGetActionOnThePaymentSettingsRoute()
        {
            var action = typeof(TenantPaymentSettingsController).GetMethod(nameof(TenantPaymentSettingsController.Get))!;

            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.PaymentsSettingsManage, authorize.Policy);
            Assert.Equal(Route, typeof(TenantPaymentSettingsController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void Put_IsAnAuthorizedPutActionOnThePaymentSettingsRoute()
        {
            var action = typeof(TenantPaymentSettingsController).GetMethod(nameof(TenantPaymentSettingsController.Put))!;

            Assert.NotNull(action.GetCustomAttribute<HttpPutAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.PaymentsSettingsManage, authorize.Policy);
            Assert.NotNull(action.GetParameters().Single(parameter => parameter.Name == "request"));
        }

        [Fact]
        public void GuestPaymentOptions_IsAnAnonymousGetActionOnTheGuestQrRoute()
        {
            var action = typeof(TenantGuestQrController).GetMethod(nameof(TenantGuestQrController.PaymentOptions))!;

            var route = Assert.IsType<HttpGetAttribute>(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Equal("{code}/payment-options", route.Template);
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var tableQr = Assert.IsType<TableQrEndpointAttribute>(action.GetCustomAttribute<TableQrEndpointAttribute>());
            Assert.True(tableQr.IsGuest);
        }

        [Fact]
        public void ThePaymentsPermissionsJoinTheCatalogAndStayGrantableToStaff()
        {
            Assert.Equal("payments.settings.manage", TenantPermissions.PaymentsSettingsManage);
            Assert.Equal("payments.online.review", TenantPermissions.PaymentsOnlineReview);
            Assert.Contains(TenantPermissions.PaymentsSettingsManage, TenantPermissions.All);
            Assert.Contains(TenantPermissions.PaymentsOnlineReview, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.PaymentsSettingsManage));
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.PaymentsOnlineReview));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.PaymentsSettingsManage));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.PaymentsOnlineReview));
        }
    }
}
