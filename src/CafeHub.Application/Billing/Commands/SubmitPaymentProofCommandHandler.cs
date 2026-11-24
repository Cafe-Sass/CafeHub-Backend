using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class SubmitPaymentProofCommandHandler(
        IPaymentStore store,
        IMediaUploadService mediaUploadService,
        ITenantContextAccessor tenantContextAccessor,
        TimeProvider timeProvider)
        : ICommandHandler<SubmitPaymentProofCommand, SubmitPaymentProofResult>
    {
        public const int MaximumPendingProofsPerBill = 3;

        public async Task<Result<SubmitPaymentProofResult>> Handle(
            SubmitPaymentProofCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var settlement = await store.FindForProofAsync(command.BillReference!, cancellationToken);
            if (settlement is null)
            {
                return Result.Failure<SubmitPaymentProofResult>(BillingErrors.NotFound());
            }

            var idempotencyKey = command.IdempotencyKey!.Trim();
            var stored = settlement.Payments.FirstOrDefault(
                payment => payment.IdempotencyKey == idempotencyKey
                    && payment.Method == PaymentMethod.Online
                    && payment.ClaimedAmount is not null);
            if (stored is not null)
            {
                return Result.Success(new SubmitPaymentProofResult(
                    stored.Id,
                    stored.Status.ToString(),
                    stored.ClaimedAmount!.Value));
            }

            var billTotal = Bill.ResolveTotalAmount(settlement.AcceptedOrders);
            var remaining = Payment.ResolveBillRemainingAmount(billTotal, settlement.Payments);
            if (command.ClaimedAmount > remaining)
            {
                return Result.Failure<SubmitPaymentProofResult>(BillingErrors.AmountAboveRemaining());
            }

            if (settlement.Payments.Count(
                    payment => payment.Status == PaymentStatus.PendingReview) >= MaximumPendingProofsPerBill)
            {
                return Result.Failure<SubmitPaymentProofResult>(
                    BillingErrors.PendingProofLimitReached(MaximumPendingProofsPerBill));
            }

            var tenantId = tenantContextAccessor.GetRequired().TenantId;
            var upload = await mediaUploadService.UploadAsync(
                MediaScope.ForTenant(tenantId),
                MediaPurpose.PaymentProof,
                command.Content!,
                cancellationToken);
            if (upload.IsFailure)
            {
                return Result.Failure<SubmitPaymentProofResult>(upload.Error!);
            }

            var payment = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(),
                settlement.Bill.Id,
                command.ClaimedAmount,
                upload.Value.Key.Value,
                command.Note,
                idempotencyKey,
                timeProvider.GetUtcNow());

            var add = await store.AddProofAsync(payment, cancellationToken);
            if (add.IsFailure)
            {
                return Result.Failure<SubmitPaymentProofResult>(add.Error!);
            }

            var saved = add.Value;
            return Result.Success(new SubmitPaymentProofResult(
                saved.Id,
                saved.Status.ToString(),
                saved.ClaimedAmount!.Value));
        }
    }
}
