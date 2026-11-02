using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches.DTOs
{
    /// <summary>
    /// The branch read model returned to a tenant user. It carries only the contract fields:
    /// identity, name, lifecycle status, contact details, opening-hours summary, timestamps,
    /// and an opaque concurrency token.
    /// </summary>
    public sealed class BranchResponse
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }

        public required string LifecycleStatus { get; init; }

        public required string? Address { get; init; }

        public required string? ContactPhone { get; init; }

        public required string? OpeningHoursSummary { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public required string ConcurrencyToken { get; init; }

        public static BranchResponse From(Branch branch, string concurrencyToken)
        {
            ArgumentNullException.ThrowIfNull(branch);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new BranchResponse
            {
                Id = branch.Id,
                Name = branch.Name,
                LifecycleStatus = branch.LifecycleStatus.ToString(),
                Address = branch.Address,
                ContactPhone = branch.ContactPhone,
                OpeningHoursSummary = branch.OpeningHoursSummary,
                CreatedAt = branch.CreatedAt,
                UpdatedAt = branch.UpdatedAt,
                ConcurrencyToken = concurrencyToken,
            };
        }
    }
}
