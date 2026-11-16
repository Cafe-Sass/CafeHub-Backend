using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class GetTableQrQueryHandler(ITableStore store, ICurrentTenantUser currentTenantUser)
        : IQueryHandler<GetTableQrQuery, TableQrData>
    {
        public async Task<Result<TableQrData>> Handle(GetTableQrQuery query, CancellationToken cancellationToken)
        {
            var scope = TableActor.EnsureBranchScope(currentTenantUser, query.BranchId);
            if (scope.IsFailure)
            {
                return Result.Failure<TableQrData>(scope.Error!);
            }

            var table = await store.GetQrAsync(query.BranchId, query.TableId, cancellationToken);
            return table is null
                ? Result.Failure<TableQrData>(TablesErrors.NotFound())
                : Result.Success(table);
        }
    }
}
