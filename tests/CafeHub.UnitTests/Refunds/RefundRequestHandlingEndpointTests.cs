using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Refunds.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class RefundRequestHandlingEndpointTests
    {
        [Fact]
        public void ReadsNeedRefundsViewAndChangesNeedRefundsManage()
        {
            Assert.Equal(TenantPermissions.RefundsView, Policy(nameof(TenantRefundRequestsController.List)));
            Assert.Equal(TenantPermissions.RefundsView, Policy(nameof(TenantRefundRequestsController.GetById)));
            Assert.Equal(TenantPermissions.RefundsView, Policy(nameof(TenantRefundRequestsController.Attachment)));
            Assert.Equal(TenantPermissions.RefundsManage, Policy(nameof(TenantRefundRequestsController.Assign)));
            Assert.Equal(TenantPermissions.RefundsManage, Policy(nameof(TenantRefundRequestsController.ChangeStatus)));
            Assert.Equal(
                "t/{slug}/api/v1/refund-requests",
                typeof(TenantRefundRequestsController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void HandlingRoutesMatchTheContract()
        {
            Assert.Equal(
                "{refundRequestId:guid}",
                typeof(TenantRefundRequestsController)
                    .GetMethod(nameof(TenantRefundRequestsController.GetById))!
                    .GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal(
                "{refundRequestId:guid}/assign",
                typeof(TenantRefundRequestsController)
                    .GetMethod(nameof(TenantRefundRequestsController.Assign))!
                    .GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal(
                "{refundRequestId:guid}/status",
                typeof(TenantRefundRequestsController)
                    .GetMethod(nameof(TenantRefundRequestsController.ChangeStatus))!
                    .GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal(
                "{refundRequestId:guid}/attachments/{attachmentId:guid}",
                typeof(TenantRefundRequestsController)
                    .GetMethod(nameof(TenantRefundRequestsController.Attachment))!
                    .GetCustomAttribute<HttpGetAttribute>()!.Template);
        }

        [Fact]
        public async Task AttachmentResponseIsNeverPubliclyCacheableOrSniffable()
        {
            var controller = new TenantRefundRequestsController(new AttachmentSender())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

            var response = await controller.Attachment(Guid.NewGuid(), Guid.NewGuid(), default);

            var file = Assert.IsType<FileStreamResult>(response);
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
            Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions.ToString());
        }

        private static string? Policy(string action) =>
            typeof(TenantRefundRequestsController)
                .GetMethod(action)!
                .GetCustomAttribute<TenantAuthorizeAttribute>()!
                .Policy;

        private sealed class AttachmentSender : ISender
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Assert.IsType<GetRefundRequestAttachmentQuery>(request);
                return Task.FromResult((TResponse)(object)Result.Success(
                    new RefundRequestAttachmentFile(new MemoryStream(), "application/pdf")));
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
    }
}
