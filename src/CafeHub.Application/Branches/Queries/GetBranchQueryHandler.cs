using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches.Queries
{
    public sealed class GetBranchQueryHandler : IQueryHandler<GetBranchQuery, BranchResponse>
    {
        private readonly IBranchStore branchStore;
        private readonly ICurrentTenantUser currentUser;

        public GetBranchQueryHandler(IBranchStore branchStore, ICurrentTenantUser currentUser)
        {
            this.branchStore = branchStore ?? throw new ArgumentNullException(nameof(branchStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<Result<BranchResponse>> Handle(GetBranchQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<BranchResponse>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != query.BranchId)
            {
                return Result.Failure<BranchResponse>(BranchesErrors.NotFound());
            }

            var branch = await branchStore.GetByIdAsync(query.BranchId, cancellationToken);
            if (branch is null || (scope.Value.HasValue && branch.LifecycleStatus != nameof(BranchLifecycleStatus.Active)))
            {
                return Result.Failure<BranchResponse>(BranchesErrors.NotFound());
            }

            return Result.Success(branch);
        }
    }
}
