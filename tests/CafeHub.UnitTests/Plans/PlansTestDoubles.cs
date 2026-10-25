using CafeHub.Application.Audit;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Domain.Plans;

namespace CafeHub.UnitTests.Plans
{
    internal sealed class FakePlanRepository : IPlanRepository
    {
        public List<Plan> Plans { get; } = new();

        public List<PlanListRequest> ListRequests { get; } = new();

        public int KeyExistsCallCount { get; private set; }

        public int FindByIdCallCount { get; private set; }

        public int PlanVersionExistsCallCount { get; private set; }
        public int FindVersionByIdCallCount { get; private set; }
        public bool PlanVersionsExist { get; set; } = true;

        public int? TotalCount { get; set; }

        public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken)
        {
            KeyExistsCallCount++;
            return Task.FromResult(Plans.Any(plan => string.Equals(plan.Code, key, StringComparison.Ordinal)));
        }

        public Task<bool> PlanVersionExistsAsync(Guid planVersionId, CancellationToken cancellationToken)
        {
            PlanVersionExistsCallCount++;
            return Task.FromResult(PlanVersionsExist);
        }

        public Task<PlanVersion?> FindVersionByIdAsync(Guid planVersionId, CancellationToken cancellationToken)
        {
            FindVersionByIdCallCount++;
            return Task.FromResult(Plans.SelectMany(plan => plan.Versions).FirstOrDefault(version => version.Id == planVersionId));
        }

        public void Add(Plan plan)
        {
            Plans.Add(plan);
        }

        public Task<Plan?> FindByIdWithVersionsAsync(Guid planId, CancellationToken cancellationToken)
        {
            FindByIdCallCount++;
            return Task.FromResult(Plans.SingleOrDefault(plan => plan.Id == planId));
        }

        public Task<PlanListPage> ListAsync(PlanListRequest request, CancellationToken cancellationToken)
        {
            ListRequests.Add(request);
            return Task.FromResult(new PlanListPage(Plans, TotalCount));
        }
    }

    internal sealed class RecordingPlanAuditWriter : IPlatformAuditWriter
    {
        public List<PlatformAuditEntry> Entries { get; } = new();

        public void Write(PlatformAuditEntry entry)
        {
            Entries.Add(entry);
        }
    }
}
