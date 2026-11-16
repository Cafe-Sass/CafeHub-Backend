using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class GetTableQueryHandler : IQueryHandler<GetTableQuery, TableResponse>
    {
        private readonly ITableStore tableStore;
        private readonly ICurrentTenantUser currentUser;

        public GetTableQueryHandler(ITableStore tableStore, ICurrentTenantUser currentUser)
        {
            this.tableStore = tableStore ?? throw new ArgumentNullException(nameof(tableStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<Result<TableResponse>> Handle(GetTableQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<TableResponse>(scope.Error!);
            }

            var table = await tableStore.GetByIdAsync(query.TableId, scope.Value, cancellationToken);
            return table is null
                ? Result.Failure<TableResponse>(TablesErrors.NotFound())
                : Result.Success(table);
        }
    }
}
