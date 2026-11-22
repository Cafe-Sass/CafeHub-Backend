namespace CafeHub.Application.Orders
{
    public sealed record MissHolderCandidate(Guid UserId, IReadOnlyList<Guid>? StationIds);

    public static class OrderResponseMissHolders
    {
        /// <summary>
        /// Selects the staff who held orders.decide when a miss was recorded. The caller already filtered the
        /// candidates to the active staff of the branch holding the permission; this applies the station rule:
        /// staff without station assignments are always eligible, staff with assignments need a visible station
        /// matching a routed item station, and an unrouted item belongs to everyone.
        /// </summary>
        public static IReadOnlyList<Guid> Resolve(
            IReadOnlyList<MissHolderCandidate> candidates,
            IReadOnlyList<Guid?> itemStationIds)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(itemStationIds);

            if (candidates.Count == 0 || itemStationIds.Count == 0)
            {
                return [];
            }

            var routedStations = itemStationIds
                .Where(stationId => stationId is not null)
                .Select(stationId => stationId!.Value)
                .ToHashSet();
            var hasUnroutedItem = itemStationIds.Any(stationId => stationId is null);

            return [.. candidates
                .Where(candidate => candidate.StationIds is null
                    || hasUnroutedItem
                    || candidate.StationIds.Any(stationId => routedStations.Contains(stationId)))
                .Select(candidate => candidate.UserId)
                .Distinct()];
        }
    }
}
