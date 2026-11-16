namespace CafeHub.Application.Stations.Contracts
{
    /// <summary>
    /// Scope contract for the stations a staff member may see. Returns null when the staff member has no
    /// station assignments, meaning every active station of their branch is visible. Returns the assigned
    /// active station ids otherwise. The branch is resolved from the staff user's own branch; an owner or
    /// user without a branch resolves to null (all stations of the requested branch).
    /// </summary>
    public interface IStationScope
    {
        Task<IReadOnlyList<Guid>?> GetVisibleStationIdsAsync(Guid userId, CancellationToken cancellationToken);
    }
}
