using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Queries
{
    public sealed class GetBranchQueryHandler : IQueryHandler<GetBranchQuery, BranchResponse>
    {
        private readonly IBranchStore branchStore;

        public GetBranchQueryHandler(IBranchStore branchStore)
        {
            this.branchStore = branchStore ?? throw new ArgumentNullException(nameof(branchStore));
        }

        public async Task<Result<BranchResponse>> Handle(GetBranchQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var branch = await branchStore.GetByIdAsync(query.BranchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<BranchResponse>(BranchesErrors.NotFound());
            }

            return Result.Success(branch);
        }
    }
}
