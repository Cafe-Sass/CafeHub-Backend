using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Refunds.Commands;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Refunds.Queries;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Controllers
{
    [ApiController]
    [Route("t/{slug}/api/v1/refund-requests")]
    public sealed class TenantRefundRequestsController(
        ISender sender,
        ICorrelationIdAccessor? correlationIdAccessor = null)
        : ControllerBase
    {
        [HttpGet]
        [TenantAuthorize(TenantPermissions.RefundsView)]
        public async Task<ActionResult<OffsetPage<RefundRequestListItem>>> List(
            [FromQuery] GetRefundRequestsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{refundRequestId:guid}")]
        [TenantAuthorize(TenantPermissions.RefundsView)]
        public async Task<ActionResult<RefundRequestDetail>> GetById(
            Guid refundRequestId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetRefundRequestQuery(refundRequestId), cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{refundRequestId:guid}/assign")]
        [TenantAuthorize(TenantPermissions.RefundsManage)]
        public async Task<ActionResult<AssignRefundRequestResult>> Assign(
            Guid refundRequestId,
            [FromBody] AssignRefundRequestRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new AssignRefundRequestCommand
                {
                    RefundRequestId = refundRequestId,
                    AssignedUserId = request.AssignedUserId,
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpPost("{refundRequestId:guid}/status")]
        [TenantAuthorize(TenantPermissions.RefundsManage)]
        public async Task<ActionResult<ChangeRefundRequestStatusResult>> ChangeStatus(
            Guid refundRequestId,
            [FromBody] ChangeRefundRequestStatusRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ChangeRefundRequestStatusCommand
                {
                    RefundRequestId = refundRequestId,
                    Status = request.Status,
                    Note = request.Note,
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ToProblemResult(HttpContext, correlationIdAccessor);
        }

        [HttpGet("{refundRequestId:guid}/attachments/{attachmentId:guid}")]
        [TenantAuthorize(TenantPermissions.RefundsView)]
        public async Task<ActionResult> Attachment(
            Guid refundRequestId,
            Guid attachmentId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetRefundRequestAttachmentQuery(refundRequestId, attachmentId),
                cancellationToken);

            if (result.IsFailure)
            {
                return result.ToProblemResult(HttpContext, correlationIdAccessor);
            }

            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(result.Value.Content, result.Value.ContentType);
        }
    }
}
