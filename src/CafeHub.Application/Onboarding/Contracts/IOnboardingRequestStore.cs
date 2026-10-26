using CafeHub.Application.Pagination;
using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Persistence port for public onboarding requests, implemented in Infrastructure against the Master database.
    /// </summary>
    public interface IOnboardingRequestStore
    {
        Task<OnboardingRequest?> FindPublicByEmailAsync(string emailLower, CancellationToken cancellationToken);
        Task<OnboardingRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
        Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken);
        Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken);
        Task<OnboardingRequestListPage> ListForReviewAsync(OnboardingRequestListRequest request, CancellationToken cancellationToken);
    }
}
