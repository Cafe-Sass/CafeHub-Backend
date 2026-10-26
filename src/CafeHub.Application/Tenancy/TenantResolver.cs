using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Tenancy
{
    /// <summary>
    /// Resolves a raw slug against the tenant registry. The slug policy validates the lowercased input;
    /// a slug that fails the policy and a slug that the registry does not know return the identical
    /// uniform unavailable result, and nothing here touches the tenant context.
    /// </summary>
    public sealed class TenantResolver : ITenantResolver
    {
        private readonly ITenantRegistryReader registryReader;

        public TenantResolver(ITenantRegistryReader registryReader)
        {
            this.registryReader = registryReader ?? throw new ArgumentNullException(nameof(registryReader));
        }

        public async Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rawSlug))
            {
                return Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable());
            }

            TenantSlug slug;
            try
            {
                slug = TenantSlug.Create(rawSlug.ToLowerInvariant());
            }
            catch (ArgumentException)
            {
                return Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable());
            }

            var registration = await registryReader.FindBySlugAsync(slug.Value, cancellationToken);
            if (registration is null)
            {
                return Result.Failure<ResolvedTenant>(TenancyErrors.TenantUnavailable());
            }

            return Result.Success(new ResolvedTenant(
                registration.TenantId,
                registration.Slug,
                registration.State,
                registration.SecretReference));
        }
    }
}
