using System.Linq;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Branches.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Branches
{
    internal sealed class FakeBranchStore : IBranchStore
    {
        public List<BranchResponseCall> Created { get; } = new();
        public List<UpdateBranchCall> Updated { get; } = new();
        public List<BranchListRequest> ListRequests { get; } = new();
        public List<Guid> GetByIdRequests { get; } = new();
        public List<TenantAuditEntry> AuditEntries { get; } = new();
        public BranchResponse? CreatedResponse { get; set; }
        public BranchResponse? UpdatedResponse { get; set; }
        public BranchResponse? GetByIdResponse { get; set; }
        public List<BranchResponse> ListResponses { get; } = new();
        public Error? Failure { get; set; }

        public Task<Result<BranchResponse>> CreateAsync(
            string name,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Created.Add(new BranchResponseCall(
                name,
                address,
                contactPhone,
                openingHoursSummary,
                actorUserId));

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure<BranchResponse>(failure));
            }

            AuditEntries.Add(new TenantAuditEntry
            {
                Action = "branches.created.succeeded",
                TargetType = "branches",
                TargetId = Guid.CreateVersion7(),
                ActorUserId = actorUserId,
            });

            var now = DateTimeOffset.UtcNow;
            var branch = Branch.Create(
                Guid.CreateVersion7(),
                name,
                BranchLifecycleStatus.Active,
                address,
                contactPhone,
                openingHoursSummary,
                now,
                actorUserId);
            return Task.FromResult(Result.Success(CreatedResponse ?? BranchResponse.From(branch, "12345")));
        }

        public Task<Result<BranchResponse>> UpdateAsync(
            Guid branchId,
            string name,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Updated.Add(new UpdateBranchCall(
                branchId,
                name,
                address,
                contactPhone,
                openingHoursSummary,
                concurrencyToken,
                actorUserId));

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure<BranchResponse>(failure));
            }

            AuditEntries.Add(new TenantAuditEntry
            {
                Action = "branches.updated.succeeded",
                TargetType = "branches",
                TargetId = branchId,
                ActorUserId = actorUserId,
            });

            return Task.FromResult(Result.Success(UpdatedResponse ?? BranchResponse.From(
                Branch.Create(branchId, name, BranchLifecycleStatus.Active, address, contactPhone, openingHoursSummary, DateTimeOffset.UtcNow, actorUserId),
                "67890")));
        }

        public Task<BranchResponse?> GetByIdAsync(Guid branchId, CancellationToken cancellationToken)
        {
            GetByIdRequests.Add(branchId);
            return Task.FromResult(GetByIdResponse);
        }

        public Task<BranchListPage> ListBranchesAsync(BranchListRequest request, CancellationToken cancellationToken)
        {
            ListRequests.Add(request);
            var items = ListResponses.Take(request.PageSize + 1).ToList();
            var totalCount = request.IncludeTotalCount ? (int?)ListResponses.Count : null;
            return Task.FromResult(new BranchListPage(items, totalCount));
        }

        public List<LifecycleChangeCall> LifecycleChanges { get; } = new();

        public Task<Result<BranchResponse>> ChangeLifecycleStatusAsync(
            Guid branchId,
            BranchLifecycleStatus newStatus,
            string concurrencyToken,
            Guid actorUserId,
            string auditAction,
            CancellationToken cancellationToken)
        {
            if (!uint.TryParse(concurrencyToken, out _))
            {
                return Task.FromResult(Result.Failure<BranchResponse>(
                    ValidationError.Create(new[] { new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.") })));
            }

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure<BranchResponse>(failure));
            }

            LifecycleChanges.Add(new LifecycleChangeCall(branchId, newStatus, concurrencyToken, actorUserId, auditAction));

            return Task.FromResult(Result.Success(UpdatedResponse ?? BranchResponse.From(
                Branch.Create(branchId, "Branch", newStatus, null, null, null, DateTimeOffset.UtcNow, actorUserId),
                "67890")));
        }
    }

    internal sealed record BranchResponseCall(
        string Name,
        string? Address,
        string? ContactPhone,
        string? OpeningHoursSummary,
        Guid ActorUserId);

    internal sealed record UpdateBranchCall(
        Guid BranchId,
        string Name,
        string? Address,
        string? ContactPhone,
        string? OpeningHoursSummary,
        string ConcurrencyToken,
        Guid ActorUserId);

    internal sealed record LifecycleChangeCall(
        Guid BranchId,
        BranchLifecycleStatus NewStatus,
        string ConcurrencyToken,
        Guid ActorUserId,
        string AuditAction);

    internal sealed class RecordingTenantAuditWriter : ITenantAuditWriter
    {
        public List<TenantAuditEntry> Entries { get; } = new();
        public Result Failure { get; set; } = Result.Success();

        public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.FromResult(Failure);
        }
    }

    internal sealed class FakeCurrentTenantUser : ICurrentTenantUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid? UserId { get; set; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public Guid? TenantId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public IReadOnlyCollection<string> Roles { get; set; } = new[] { "Owner" };
    }

    internal sealed class StubTenantContextAccessor(TenantContext tenantContext) : ITenantContextAccessor
    {
        public TenantContext GetRequired() => tenantContext;
    }

    internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
