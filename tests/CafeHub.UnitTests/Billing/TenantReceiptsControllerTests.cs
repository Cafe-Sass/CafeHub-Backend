using System.Reflection;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Billing
{
    public sealed class TenantReceiptsControllerTests
    {
        private const string StaffRoute = "t/{slug}/api/v1/receipts";
        private const string GuestRoute = "t/{slug}/api/v1/guest/receipts";

        [Fact]
        public void StaffReceiptActionsRequireBillsViewAndTheExpectedRoutes()
        {
            var byId = typeof(TenantReceiptsController).GetMethod(nameof(TenantReceiptsController.GetById))!;
            var byBill = typeof(TenantBillsController).GetMethod(nameof(TenantBillsController.GetReceipt))!;

            Assert.Equal(StaffRoute, typeof(TenantReceiptsController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal("{receiptId:guid}", Assert.IsType<HttpGetAttribute>(
                byId.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.Equal(
                TenantPermissions.BillsView,
                Assert.IsType<TenantAuthorizeAttribute>(byId.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.Equal("{billId:guid}/receipt", Assert.IsType<HttpGetAttribute>(
                byBill.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.Equal(
                TenantPermissions.BillsView,
                Assert.IsType<TenantAuthorizeAttribute>(byBill.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.Null(byBill.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void GuestReceiptActionIsAnonymousRateLimitedAndAllowListed()
        {
            var action = typeof(TenantGuestReceiptsController).GetMethod(
                nameof(TenantGuestReceiptsController.Get))!;

            Assert.Equal(GuestRoute, typeof(TenantGuestReceiptsController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal("{billReference}", Assert.IsType<HttpGetAttribute>(
                action.GetCustomAttribute<HttpGetAttribute>()).Template);
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            var tableQr = Assert.IsType<TableQrEndpointAttribute>(
                action.GetCustomAttribute<TableQrEndpointAttribute>());
            Assert.True(tableQr.IsGuest);
            Assert.Contains(
                action.GetCustomAttributes<ProducesResponseTypeAttribute>(),
                response => response.StatusCode == StatusCodes.Status429TooManyRequests);
            Assert.Contains(
                action.GetCustomAttributes<ProducesResponseTypeAttribute>(),
                response => response.StatusCode == StatusCodes.Status404NotFound);
        }
    }
}
