using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed class PayOnboardingRequestCommandHandler : ICommandHandler<PayOnboardingRequestCommand, PaymentOutcomeResponse>
    {
        private const string SucceededAction = "onboarding.payment.succeeded";
        private const string FailedAction = "onboarding.payment.failed";
        private const string TargetType = "onboarding_request";

        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly IPaymentTransactionStore paymentTransactionStore;
        private readonly IPaymentGateway paymentGateway;
        private readonly IPlanRepository planRepository;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly TimeProvider timeProvider;
        private readonly IDuplicateRegistrationDetector duplicateRegistrationDetector;

        public PayOnboardingRequestCommandHandler(
            IOnboardingRequestStore onboardingRequestStore,
            IPaymentTransactionStore paymentTransactionStore,
            IPaymentGateway paymentGateway,
            IPlanRepository planRepository,
            IMasterUnitOfWork unitOfWork,
            IPlatformAuditWriter auditWriter,
            TimeProvider timeProvider,
            IDuplicateRegistrationDetector duplicateRegistrationDetector)
        {
            this.onboardingRequestStore = onboardingRequestStore;
            this.paymentTransactionStore = paymentTransactionStore;
            this.paymentGateway = paymentGateway;
            this.planRepository = planRepository;
            this.unitOfWork = unitOfWork;
            this.auditWriter = auditWriter;
            this.timeProvider = timeProvider;
            this.duplicateRegistrationDetector = duplicateRegistrationDetector;
        }

        public async Task<Result<PaymentOutcomeResponse>> Handle(PayOnboardingRequestCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var now = timeProvider.GetUtcNow();

            var request = await onboardingRequestStore.FindByIdAsync(command.RequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<PaymentOutcomeResponse>(OnboardingErrors.RequestNotFound());
            }

            var existingTransaction = await paymentTransactionStore.FindByIdempotencyKeyAsync(command.RequestId, command.IdempotencyKey!, cancellationToken);
            if (existingTransaction is not null)
            {
                return Result.Success(new PaymentOutcomeResponse(command.RequestId, request.Status, existingTransaction.Status));
            }

            if (request.Status != OnboardingRequestStatus.AwaitingPayment)
            {
                return Result.Failure<PaymentOutcomeResponse>(OnboardingErrors.RequestNotPayable());
            }

            var planVersion = await planRepository.FindVersionByIdAsync(request.PlanVersionId, cancellationToken);
            if (planVersion is null || planVersion.Status != PlanVersionStatus.Active)
            {
                return Result.Failure<PaymentOutcomeResponse>(OnboardingErrors.PlanVersionNotFound());
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var chargeRequest = new PaymentChargeRequest(
                    planVersion.Price,
                    planVersion.CurrencyCode,
                    command.IdempotencyKey!,
                    command.RequestId.ToString());

                var chargeResult = await paymentGateway.ChargeAsync(chargeRequest, cancellationToken);

                if (!chargeResult.IsSuccess)
                {
                    return Result.Failure<PaymentOutcomeResponse>(chargeResult.Error!);
                }

                var chargeValue = chargeResult.Value;
                var transactionStatus = chargeValue.Outcome == PaymentOutcome.Succeeded
                    ? PlatformPaymentTransactionStatus.Succeeded
                    : PlatformPaymentTransactionStatus.Failed;

                var transaction = PlatformPaymentTransaction.Create(
                    Guid.CreateVersion7(),
                    command.RequestId,
                    null,
                    command.IdempotencyKey!,
                    planVersion.Price,
                    planVersion.CurrencyCode,
                    transactionStatus,
                    chargeValue.Outcome == PaymentOutcome.Succeeded ? now : null,
                    chargeValue.FailureCode,
                    now);

                await paymentTransactionStore.AddAsync(transaction, cancellationToken);

                if (chargeValue.Outcome == PaymentOutcome.Succeeded)
                {
                    request.TransitionTo(OnboardingRequestStatus.PaidAwaitingReview, now);

                    var subscription = Subscription.Create(
                        Guid.CreateVersion7(),
                        command.RequestId,
                        null,
                        SubscriptionStatus.Active,
                        now);

                    var period = SubscriptionPeriod.Create(
                        Guid.CreateVersion7(),
                        subscription.Id,
                        planVersion.Id,
                        1,
                        SubscriptionPeriodStatus.Active,
                        planVersion.Price,
                        planVersion.CurrencyCode,
                        now,
                        null,
                        null,
                        now);

                    await onboardingRequestStore.UpdateAsync(request, cancellationToken);

                    auditWriter.Write(new PlatformAuditEntry
                    {
                        ActorKind = PlatformAuditActorKind.System,
                        Action = SucceededAction,
                        TargetType = TargetType,
                        TargetId = command.RequestId,
                        Outcome = PlatformAuditOutcome.Succeeded,
                    });

                    await unitOfWork.CommitTransactionAsync(cancellationToken);

                    return Result.Success(new PaymentOutcomeResponse(command.RequestId, request.Status, transactionStatus));
                }

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = FailedAction,
                    TargetType = TargetType,
                    TargetId = command.RequestId,
                    Outcome = PlatformAuditOutcome.Failed,
                    Reason = chargeValue.FailureCode,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Success(new PaymentOutcomeResponse(command.RequestId, request.Status, transactionStatus));
            }
            catch (Exception exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);

                if (duplicateRegistrationDetector.IsIdempotencyKeyViolation(exception))
                {
                    var existing = await paymentTransactionStore.FindByIdempotencyKeyAsync(command.RequestId, command.IdempotencyKey!, CancellationToken.None);
                    if (existing is not null)
                    {
                        return Result.Success(new PaymentOutcomeResponse(command.RequestId, request.Status, existing.Status));
                    }
                }

                throw;
            }
        }
    }
}
