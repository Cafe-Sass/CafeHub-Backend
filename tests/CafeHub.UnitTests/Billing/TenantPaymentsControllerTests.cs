using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Billing
{
    public sealed class TenantPaymentsControllerTests
    {
        private const string Route = "t/{slug}/api/v1/payments";

        [Fact]
        public void Actions_RequireTheOnlineReviewPermissionOnThePaymentsRoute()
        {
            Assert.Equal(Route, typeof(TenantPaymentsController).GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.Equal(
                TenantPermissions.PaymentsOnlineReview,
                Authorize(nameof(TenantPaymentsController.List)).Policy);
            Assert.Equal(
                TenantPermissions.PaymentsOnlineReview,
                Authorize(nameof(TenantPaymentsController.Proof)).Policy);
            Assert.Equal(
                TenantPermissions.PaymentsOnlineReview,
                Authorize(nameof(TenantPaymentsController.Confirm)).Policy);
            Assert.Equal(
                TenantPermissions.PaymentsOnlineReview,
                Authorize(nameof(TenantPaymentsController.Reject)).Policy);
            Assert.Null(typeof(TenantPaymentsController)
                .GetMethod(nameof(TenantPaymentsController.List))!
                .GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void Actions_UseTheReviewRoutesAndBodies()
        {
            Assert.Equal("{paymentId:guid}/proof", Template<TenantPaymentsController>(
                nameof(TenantPaymentsController.Proof), typeof(HttpGetAttribute)));
            Assert.Equal("{paymentId:guid}/confirm", Template<TenantPaymentsController>(
                nameof(TenantPaymentsController.Confirm), typeof(HttpPostAttribute)));
            Assert.Equal("{paymentId:guid}/reject", Template<TenantPaymentsController>(
                nameof(TenantPaymentsController.Reject), typeof(HttpPostAttribute)));

            Assert.NotNull(typeof(ConfirmPaymentRequest).GetProperty(nameof(ConfirmPaymentRequest.Amount)));
            Assert.Equal(typeof(string), typeof(RejectPaymentRequest).GetProperty(nameof(RejectPaymentRequest.Reason))!.PropertyType);
        }

        [Fact]
        public async Task Proof_SetsTheNoStoreAndNosniffHeadersForTheStoredImage()
        {
            using var content = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
            var controller = new TenantPaymentsController(
                new ProofSender(new PaymentProofFile(content, "image/png")))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            var result = await controller.Proof(Guid.CreateVersion7(), default);

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("image/png", file.ContentType);
            Assert.Equal("no-store", controller.Response.Headers.CacheControl);
            Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions);
        }

        private static TenantAuthorizeAttribute Authorize(string methodName) =>
            Assert.IsType<TenantAuthorizeAttribute>(
                typeof(TenantPaymentsController).GetMethod(methodName)!.GetCustomAttribute<TenantAuthorizeAttribute>());

        private static string? Template<TController>(string methodName, Type attributeType) =>
            attributeType == typeof(HttpGetAttribute)
                ? typeof(TController).GetMethod(methodName)!.GetCustomAttribute<HttpGetAttribute>()!.Template
                : typeof(TController).GetMethod(methodName)!.GetCustomAttribute<HttpPostAttribute>()!.Template;

        private sealed class ProofSender(PaymentProofFile proof) : ISender
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                if (request is GetPaymentProofQuery && typeof(TResponse) == typeof(Result<PaymentProofFile>))
                {
                    return Task.FromResult((TResponse)(object)Result.Success(proof));
                }

                throw new NotSupportedException("Only the proof query is handled.");
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("Only the typed send is used.");

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("Only the response typed send is used.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("Streams are not used.");

            public IAsyncEnumerable<object?> CreateStream(
                object request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("Streams are not used.");
        }
    }
}
