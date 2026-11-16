using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class GetTableQrSheetQueryHandler(ITableStore store, ICurrentTenantUser currentTenantUser)
        : IQueryHandler<GetTableQrSheetQuery, IReadOnlyList<TableQrData>>
    {
        public async Task<Result<IReadOnlyList<TableQrData>>> Handle(GetTableQrSheetQuery query, CancellationToken cancellationToken)
        {
            var scope = TableActor.EnsureBranchScope(currentTenantUser, query.BranchId);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<TableQrData>>(scope.Error!);
            }

            var sheet = await store.GetQrSheetAsync(query.BranchId, cancellationToken);
            return sheet is null
                ? Result.Failure<IReadOnlyList<TableQrData>>(TablesErrors.NotFound())
                : Result.Success(sheet);
        }
    }
}
