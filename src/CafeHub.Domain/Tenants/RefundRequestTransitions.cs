namespace CafeHub.Domain.Tenants
{
    public static class RefundRequestTransitions
    {
        private static readonly IReadOnlyDictionary<RefundRequestStatus, IReadOnlySet<RefundRequestStatus>> AllowedTargets =
            new Dictionary<RefundRequestStatus, IReadOnlySet<RefundRequestStatus>>
            {
                [RefundRequestStatus.Open] = new HashSet<RefundRequestStatus>
                {
                    RefundRequestStatus.InReview,
                    RefundRequestStatus.Approved,
                    RefundRequestStatus.Rejected,
                },
                [RefundRequestStatus.InReview] = new HashSet<RefundRequestStatus>
                {
                    RefundRequestStatus.Approved,
                    RefundRequestStatus.Rejected,
                },
                [RefundRequestStatus.Approved] = new HashSet<RefundRequestStatus>(),
                [RefundRequestStatus.Rejected] = new HashSet<RefundRequestStatus>(),
            };

        public static bool IsAllowed(RefundRequestStatus current, RefundRequestStatus target) =>
            AllowedTargets.TryGetValue(current, out var targets) && targets.Contains(target);
    }
}
