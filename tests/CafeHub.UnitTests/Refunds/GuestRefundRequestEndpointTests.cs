using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Refunds.Commands;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class GuestRefundRequestEndpointTests
    {
        [Fact]
        public void BillPaymentAndRefundSubmissionActionsAreAnonymousGuestEndpoints()
        {
            var actions = typeof(TenantGuestBillsController).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var paymentList = actions.SingleOrDefault(action =>
                action.GetCustomAttribute<HttpGetAttribute>()?.Template == "{billReference}/payments");
            var submit = actions.SingleOrDefault(action =>
                action.GetCustomAttribute<HttpPostAttribute>()?.Template == "{billReference}/refund-requests");

            AssertGuestAction(paymentList);
            AssertGuestAction(submit);
            Assert.Equal("t/{slug}/api/v1/guest/bills", typeof(TenantGuestBillsController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void RefundStatusActionIsAnAnonymousGuestEndpoint()
        {
            var controllerType = typeof(TenantGuestBillsController).Assembly
                .GetType("CafeHub.WebApi.Controllers.TenantGuestRefundRequestsController");
            Assert.NotNull(controllerType);

            var action = controllerType!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(method => method.GetCustomAttribute<HttpGetAttribute>()?.Template == "{publicReference}");

            AssertGuestAction(action);
            Assert.Equal("t/{slug}/api/v1/guest/refund-requests", controllerType.GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void RefundStatusResponseContainsOnlyTheGuestProjection()
        {
            Assert.Equal(
                new[]
                {
                    nameof(GuestRefundRequestStatus.PublicReference),
                    nameof(GuestRefundRequestStatus.Status),
                    nameof(GuestRefundRequestStatus.ResolutionNote),
                },
                typeof(GuestRefundRequestStatus).GetProperties()
                    .Select(property => property.Name)
                    .ToArray());
        }

        [Fact]
        public async Task SubmissionResponseContainsOnlyPublicReferenceAndStatus()
        {
            var controller = new TenantGuestBillsController(new SubmissionSender(), new NoCorrelationId())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            var response = await controller.SubmitRefundRequest(
                "cafe",
                "Abcdefghijklmnopqrstuv",
                new SubmitGuestRefundRequestRequest(Guid.NewGuid(), 12m, "Reason", null, null, null),
                "refund-key-1",
                default);

            var created = Assert.IsType<CreatedResult>(response.Result);
            var body = Assert.IsType<SubmitGuestRefundRequestResult>(created.Value);
            Assert.Equal("ZYXWVUTSRQPONMLKJIHGFE", body.PublicReference);
            Assert.Equal("Open", body.Status);
            Assert.Equal(
                new[] { nameof(SubmitGuestRefundRequestResult.PublicReference), nameof(SubmitGuestRefundRequestResult.Status) },
                typeof(SubmitGuestRefundRequestResult).GetProperties().Select(property => property.Name).ToArray());
            Assert.Equal("/t/cafe/api/v1/guest/refund-requests/ZYXWVUTSRQPONMLKJIHGFE", created.Location);
        }

        private static void AssertGuestAction(MethodInfo? action)
        {
            Assert.NotNull(action);
            Assert.NotNull(action!.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.True(action.GetCustomAttribute<TableQrEndpointAttribute>()!.IsGuest);
            Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
        }

        private sealed class SubmissionSender : ISender
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Assert.IsType<SubmitGuestRefundRequestCommand>(request);
                return Task.FromResult((TResponse)(object)Result.Success(
                    new SubmitGuestRefundRequestResult("ZYXWVUTSRQPONMLKJIHGFE", "Open")));
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest => throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(
                object request,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class NoCorrelationId : ICorrelationIdAccessor
        {
            public string? CorrelationId => null;
        }
    }
}
