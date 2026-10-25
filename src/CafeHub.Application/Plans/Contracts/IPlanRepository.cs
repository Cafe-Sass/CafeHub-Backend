using CafeHub.Domain.Plans;

namespace CafeHub.Application.Plans.Contracts
{
    /// <summary>
    /// The plans module persistence port, implemented in Infrastructure against the Master database.
    /// <see cref="FindByIdWithVersionsAsync"/> loads a tracked aggregate so the version command can append a version;
    /// the list read is no-tracking.
    /// </summary>
    public interface IPlanRepository
    {
        Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken);

        Task<bool> PlanVersionExistsAsync(Guid planVersionId, CancellationToken cancellationToken);

        Task<PlanVersion?> FindVersionByIdAsync(Guid planVersionId, CancellationToken cancellationToken);

        void Add(Plan plan);

        Task<Plan?> FindByIdWithVersionsAsync(Guid planId, CancellationToken cancellationToken);

        Task<PlanListPage> ListAsync(PlanListRequest request, CancellationToken cancellationToken);
    }
}
