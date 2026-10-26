using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Onboarding.Queries
{
    public sealed class GetOnboardingRequestQueryHandler : IQueryHandler<GetOnboardingRequestQuery, OnboardingRequestDetail>
    {
        private readonly IOnboardingRequestStore onboardingRequestStore;
        private readonly IPlanRepository planRepository;
        private readonly IPaymentTransactionStore paymentTransactionStore;

        public GetOnboardingRequestQueryHandler(
            IOnboardingRequestStore onboardingRequestStore,
            IPlanRepository planRepository,
            IPaymentTransactionStore paymentTransactionStore)
        {
            this.onboardingRequestStore = onboardingRequestStore ?? throw new ArgumentNullException(nameof(onboardingRequestStore));
            this.planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            this.paymentTransactionStore = paymentTransactionStore ?? throw new ArgumentNullException(nameof(paymentTransactionStore));
        }

        public async Task<Result<OnboardingRequestDetail>> Handle(GetOnboardingRequestQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var request = await onboardingRequestStore.FindByIdAsync(query.RequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<OnboardingRequestDetail>(OnboardingErrors.RequestNotFound());
            }

            PlanVersion? planVersion = null;
            Plan? plan = null;
            if (request.PlanVersionId != Guid.Empty)
            {
                planVersion = await planRepository.FindVersionByIdAsync(request.PlanVersionId, cancellationToken);
                if (planVersion is not null)
                {
                    plan = await planRepository.FindByIdWithVersionsAsync(planVersion.PlanId, cancellationToken);
                }
            }

            var payment = await paymentTransactionStore.FindByOnboardingRequestIdAsync(request.Id, cancellationToken);

            return Result.Success(OnboardingRequestDetail.From(request, planVersion, plan, payment));
        }
    }
}
