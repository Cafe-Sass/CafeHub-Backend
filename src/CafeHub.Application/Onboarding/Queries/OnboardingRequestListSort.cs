using CafeHub.Application.Pagination;

namespace CafeHub.Application.Onboarding.Queries
{
    /// <summary>
    /// The onboarding review queue sort contract: <c>createdAt</c> and <c>state</c> are requestable, and every
    /// resolved sort ends with the identifier tiebreaker. The default is <c>createdAt ASC, id ASC</c>, the
    /// oldest-pending-first queue order named by <c>architecture/indexing-and-pagination-strategy.md</c>
    /// (section 4.3).
    /// </summary>
    public static class OnboardingRequestListSort
    {
        public const string CreatedAtField = "createdAt";
        public const string StateField = "state";

        public static SortWhitelist Whitelist { get; } = SortWhitelist.Create(
            new[] { CreatedAtField, StateField, SortWhitelist.IdentifierField },
            CreatedAtField,
            SortDirection.Asc);
    }
}
