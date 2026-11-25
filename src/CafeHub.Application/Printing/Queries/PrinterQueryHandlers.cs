using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;

namespace CafeHub.Application.Printing.Queries
{
    public sealed class ListPrintersQueryHandler(
        IPrinterStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<ListPrintersQuery, IReadOnlyList<PrinterResponse>>
    {
        public async Task<Result<IReadOnlyList<PrinterResponse>>> Handle(
            ListPrintersQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<PrinterResponse>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<PrinterResponse>>(PrintingErrors.BranchRequired());
            }

            return Result.Success(await store.ListAsync(branchId.Value, cancellationToken));
        }
    }

    public sealed class ListPrintRoutesQueryHandler(
        IPrinterStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<ListPrintRoutesQuery, IReadOnlyList<PrintRouteResponse>>
    {
        public async Task<Result<IReadOnlyList<PrintRouteResponse>>> Handle(
            ListPrintRoutesQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<PrintRouteResponse>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<PrintRouteResponse>>(PrintingErrors.BranchRequired());
            }

            return Result.Success(await store.ListRoutesAsync(branchId.Value, cancellationToken));
        }
    }
}
