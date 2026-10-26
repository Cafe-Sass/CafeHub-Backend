using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Onboarding.Queries;

namespace CafeHub.Application.Onboarding.Queries
{
    public sealed class GetOnboardingStatusQueryHandler : IQueryHandler<GetOnboardingStatusQuery, OnboardingStatusResponse>
    {
        private readonly IOnboardingStatusReader statusReader;

        public GetOnboardingStatusQueryHandler(IOnboardingStatusReader statusReader)
        {
            this.statusReader = statusReader ?? throw new ArgumentNullException(nameof(statusReader));
        }

        public async Task<Result<OnboardingStatusResponse>> Handle(GetOnboardingStatusQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var snapshot = await statusReader.FindByRequestIdAsync(query.RequestId, cancellationToken);
            if (snapshot is null)
            {
                return Result.Failure<OnboardingStatusResponse>(OnboardingErrors.RequestNotFound());
            }

            var status = OnboardingStatusMapper.Map(snapshot.RequestStatus, snapshot.TenantStatus, snapshot.JobStatus);

            return Result.Success(new OnboardingStatusResponse(status));
        }
    }
}
