namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Slug availability check. The initial implementation consults live onboarding requests; a later story adds
    /// the tenant registry as an additional source without changing this contract.
    /// </summary>
    public interface ISlugAvailability
    {
        Task<bool> IsAvailableAsync(string slug, CancellationToken cancellationToken);
    }
}
