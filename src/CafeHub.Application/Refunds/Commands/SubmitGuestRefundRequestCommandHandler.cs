using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed class SubmitGuestRefundRequestCommandHandler(
        IRefundRequestStore store,
        IMediaUploadService mediaUploadService,
        IMediaStorage mediaStorage,
        IBillPublicReferenceGenerator referenceGenerator,
        ITenantContextAccessor tenantContextAccessor,
        TimeProvider timeProvider,
        ILogger<SubmitGuestRefundRequestCommandHandler> logger)
        : ICommandHandler<SubmitGuestRefundRequestCommand, SubmitGuestRefundRequestResult>
    {
        public const int MaximumOpenRequestsPerBill = 3;

        public async Task<Result<SubmitGuestRefundRequestResult>> Handle(
            SubmitGuestRefundRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var submission = await store.FindForSubmissionAsync(
                command.BillReference!,
                command.PaymentId,
                command.IdempotencyKey!,
                cancellationToken);
            if (submission is null)
            {
                return Result.Failure<SubmitGuestRefundRequestResult>(RefundRequestErrors.NotFound());
            }

            if (submission.IdempotencyKeyUsedByAnotherBill)
            {
                return Result.Failure<SubmitGuestRefundRequestResult>(IdempotencyErrors.KeyReused());
            }

            if (submission.ExistingRequest is { } existing)
            {
                return Result.Success(ToResult(existing));
            }

            if (submission.Payment is not { Status: PaymentStatus.Confirmed } payment
                || payment.BillId != submission.Bill.Id)
            {
                return Result.Failure<SubmitGuestRefundRequestResult>(RefundRequestErrors.NotFound());
            }

            if (submission.OpenRequestCount >= MaximumOpenRequestsPerBill)
            {
                return Result.Failure<SubmitGuestRefundRequestResult>(
                    RefundRequestErrors.OpenRequestLimitReached(MaximumOpenRequestsPerBill));
            }

            if (command.ClaimedAmount > payment.Amount - submission.ApprovedRefundedAmount)
            {
                return Result.Failure<SubmitGuestRefundRequestResult>(RefundRequestErrors.AmountAboveRefundable());
            }

            var now = timeProvider.GetUtcNow();
            var request = RefundRequest.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                submission.Bill,
                payment,
                referenceGenerator.Generate(),
                command.IdempotencyKey!,
                command.Reason!,
                command.Details,
                command.ClaimedAmount,
                submission.ApprovedRefundedAmount,
                command.GuestDisplayName,
                now);

            var uploadedKeys = new List<MediaKey>();
            var keepUploads = false;
            try
            {
                var scope = MediaScope.ForTenant(tenantContextAccessor.GetRequired().TenantId);
                foreach (var evidence in command.Evidence)
                {
                    var upload = await mediaUploadService.UploadAsync(
                        scope,
                        MediaPurpose.RefundEvidence,
                        evidence.Content,
                        cancellationToken);
                    if (upload.IsFailure)
                    {
                        return Result.Failure<SubmitGuestRefundRequestResult>(upload.Error!);
                    }

                    uploadedKeys.Add(upload.Value.Key);
                    var attachment = RefundRequestAttachment.Create(
                        Guid.CreateVersion7(),
                        request.Id,
                        upload.Value.Key.Value,
                        upload.Value.ContentType,
                        upload.Value.Size,
                        upload.Value.Sha256,
                        now);
                    if (!request.AddAttachment(attachment))
                    {
                        throw new InvalidOperationException("A validated refund evidence attachment could not be added.");
                    }
                }

                var added = await store.AddAsync(request, cancellationToken);
                if (added.IsFailure)
                {
                    return Result.Failure<SubmitGuestRefundRequestResult>(added.Error!);
                }

                keepUploads = !added.Value.AlreadyExisted;
                return Result.Success(ToResult(added.Value.Request));
            }
            finally
            {
                if (!keepUploads)
                {
                    await DeleteUploadsAsync(uploadedKeys);
                }
            }
        }

        private static SubmitGuestRefundRequestResult ToResult(RefundRequest request) =>
            new(request.PublicReference, request.Status.ToString());

        private async Task DeleteUploadsAsync(IEnumerable<MediaKey> uploadedKeys)
        {
            foreach (var key in uploadedKeys)
            {
                try
                {
                    await mediaStorage.DeleteAsync(key, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        "An unpersisted refund evidence upload could not be removed ({ExceptionType}).",
                        exception.GetType().Name);
                }
            }
        }
    }
}
