using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Onboarding.DTOs;

namespace CafeHub.Application.Onboarding.Queries
{
    public sealed record GetOnboardingRequestQuery(Guid RequestId) : IQuery<OnboardingRequestDetail>;
}
