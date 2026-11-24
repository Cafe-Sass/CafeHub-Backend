using System.Reflection;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Billing
{
    public sealed class TenantGuestBillsControllerTests
    {
        private const string Route = "t/{slug}/api/v1/guest/bills";

        [Fact]
        public void SubmitProof_IsAnAnonymousGuestPostThatAcceptsAnImage()
        {
            var action = typeof(TenantGuestBillsController)
                .GetMethod(nameof(TenantGuestBillsController.SubmitProof))!;

            var route = Assert.IsType<HttpPostAttribute>(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal("{billReference}/payment-proofs", route.Template);
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            var tableQr = Assert.IsType<TableQrEndpointAttribute>(action.GetCustomAttribute<TableQrEndpointAttribute>());
            Assert.True(tableQr.IsGuest);
            Assert.Equal(Route, typeof(TenantGuestBillsController).GetCustomAttribute<RouteAttribute>()!.Template);

            var request = action.GetParameters().Single(parameter => parameter.Name == "request");
            Assert.Equal(typeof(SubmitPaymentProofRequest), request.ParameterType);
            Assert.Single(request.GetCustomAttributes(typeof(FromFormAttribute), inherit: false));
            Assert.Equal(
                typeof(IFormFile),
                typeof(SubmitPaymentProofRequest).GetProperty(nameof(SubmitPaymentProofRequest.Image))!.PropertyType);
        }
    }
}
