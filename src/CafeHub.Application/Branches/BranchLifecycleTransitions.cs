using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches
{
    /// <summary>
    /// The legal branch lifecycle transition table. Transitions are application-validated; a branch is
    /// never physically deleted and Retired is terminal.
    /// </summary>
    public static class BranchLifecycleTransitions
    {
        private static readonly IReadOnlyDictionary<BranchLifecycleStatus, IReadOnlySet<BranchLifecycleStatus>> LegalTargets =
            new Dictionary<BranchLifecycleStatus, IReadOnlySet<BranchLifecycleStatus>>
            {
                [BranchLifecycleStatus.Active] = new HashSet<BranchLifecycleStatus>
                {
                    BranchLifecycleStatus.Suspended,
                    BranchLifecycleStatus.Retired,
                },
                [BranchLifecycleStatus.Suspended] = new HashSet<BranchLifecycleStatus>
                {
                    BranchLifecycleStatus.Active,
                    BranchLifecycleStatus.Retired,
                },
                [BranchLifecycleStatus.Retired] = new HashSet<BranchLifecycleStatus>(),
            };

        /// <summary>
        /// Returns true when moving from <paramref name="current"/> to <paramref name="target"/> is a legal
        /// transition. A transition to the same status is idempotency, handled by the caller before legality,
        /// and is not reported here.
        /// </summary>
        public static bool IsLegalTransition(BranchLifecycleStatus current, BranchLifecycleStatus target)
        {
            return LegalTargets.TryGetValue(current, out var targets)
                && targets.Contains(target);
        }
    }
}
