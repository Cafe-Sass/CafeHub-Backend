using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed class GetRefundRequestAttachmentQueryHandler(
        IRefundRequestStore store,
        IMediaStorage storage,
        ICurrentTenantUser currentUser,
        ITenantContextAccessor tenantContextAccessor)
        : IQueryHandler<GetRefundRequestAttachmentQuery, RefundRequestAttachmentFile>
    {
        public async Task<Result<RefundRequestAttachmentFile>> Handle(
            GetRefundRequestAttachmentQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<RefundRequestAttachmentFile>(scope.Error!);
            }

            var attachment = await store.FindAttachmentAsync(
                scope.Value,
                query.RefundRequestId,
                query.AttachmentId,
                cancellationToken);
            if (attachment is null
                || !MediaKey.TryParse(attachment.MediaKey, out var key)
                || key is null
                || key.Scope.TenantId != tenantContextAccessor.GetRequired().TenantId)
            {
                return Result.Failure<RefundRequestAttachmentFile>(RefundRequestErrors.NotFound());
            }

            var content = await storage.OpenAsync(key, cancellationToken);
            if (content is null)
            {
                return Result.Failure<RefundRequestAttachmentFile>(RefundRequestErrors.NotFound());
            }

            var detected = MediaFileValidator.Detect(content);
            if (detected is null || !MediaPurposes.Allows(MediaPurpose.RefundEvidence, detected.Value))
            {
                await content.DisposeAsync();
                return Result.Failure<RefundRequestAttachmentFile>(RefundRequestErrors.NotFound());
            }

            return Result.Success(new RefundRequestAttachmentFile(
                content,
                MediaContentTypes.ToContentType(detected.Value)));
        }
    }
}
