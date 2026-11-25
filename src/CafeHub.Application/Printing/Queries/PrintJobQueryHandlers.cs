using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Queries
{
    public sealed class ListPrintJobsQueryHandler(
        IPrintJobOperationsStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<ListPrintJobsQuery, IReadOnlyList<PrintJobResponse>>
    {
        public async Task<Result<IReadOnlyList<PrintJobResponse>>> Handle(
            ListPrintJobsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<PrintJobResponse>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<PrintJobResponse>>(PrintingErrors.BranchRequired());
            }

            if (query.PrinterId == Guid.Empty)
            {
                return Result.Failure<IReadOnlyList<PrintJobResponse>>(
                    PrintingErrors.InvalidJobFilter("printerId"));
            }

            if (!TryParseStatus(query.Status, out var status))
            {
                return Result.Failure<IReadOnlyList<PrintJobResponse>>(
                    PrintingErrors.InvalidJobFilter("status"));
            }

            if (!TryParseDocumentType(query.DocumentType, out var documentType))
            {
                return Result.Failure<IReadOnlyList<PrintJobResponse>>(
                    PrintingErrors.InvalidJobFilter("documentType"));
            }

            return Result.Success(await store.ListAsync(
                branchId.Value,
                status,
                query.PrinterId,
                documentType,
                cancellationToken));
        }

        private static bool TryParseStatus(string? value, out PrintJobStatus? status)
        {
            status = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!Enum.TryParse<PrintJobStatus>(value, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return false;
            }

            status = parsed;
            return true;
        }

        private static bool TryParseDocumentType(string? value, out PrintDocumentType? documentType)
        {
            documentType = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!Enum.TryParse<PrintDocumentType>(value, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return false;
            }

            documentType = parsed;
            return true;
        }
    }

    public sealed class ListPrintConnectorsQueryHandler(
        IPrintConnectorStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<ListPrintConnectorsQuery, IReadOnlyList<PrintConnectorResponse>>
    {
        public async Task<Result<IReadOnlyList<PrintConnectorResponse>>> Handle(
            ListPrintConnectorsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<PrintConnectorResponse>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<PrintConnectorResponse>>(
                    PrintingErrors.BranchRequired());
            }

            return Result.Success(await store.ListAsync(branchId.Value, cancellationToken));
        }
    }
}
