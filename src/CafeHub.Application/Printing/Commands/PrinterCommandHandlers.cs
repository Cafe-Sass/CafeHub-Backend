using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Commands
{
    public sealed class RegisterPrinterCommandHandler(
        IPrinterStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<RegisterPrinterCommand, PrinterResponse>
    {
        public Task<Result<PrinterResponse>> Handle(
            RegisterPrinterCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.RegisterAsync(
                    command.BranchId,
                    command.StationId,
                    command.Name!,
                    command.Profile,
                    command.ConnectionKind!,
                    actorId,
                    cancellationToken));
    }

    public sealed class UpdatePrinterCommandHandler(
        IPrinterStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<UpdatePrinterCommand, PrinterResponse>
    {
        public Task<Result<PrinterResponse>> Handle(
            UpdatePrinterCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.UpdateAsync(
                    command.PrinterId,
                    command.BranchId,
                    command.StationId,
                    command.Name!,
                    command.Profile,
                    command.ConnectionKind!,
                    command.ConcurrencyToken,
                    actorId,
                    cancellationToken));
    }

    public sealed class SetPrinterStatusCommandHandler(
        IPrinterStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<SetPrinterStatusCommand, PrinterResponse>
    {
        public Task<Result<PrinterResponse>> Handle(
            SetPrinterStatusCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.SetStatusAsync(
                    command.PrinterId,
                    command.BranchId,
                    command.Status,
                    command.ConcurrencyToken,
                    actorId,
                    cancellationToken));
    }

    public sealed class SetPrintRouteCommandHandler(
        IPrinterStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<SetPrintRouteCommand, PrintRouteResponse>
    {
        public async Task<Result<PrintRouteResponse>> Handle(
            SetPrintRouteCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<PrintRouteResponse>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != command.BranchId)
            {
                return Result.Failure<PrintRouteResponse>(PrintingErrors.NotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<PrintRouteResponse>(AuthErrors.Forbidden());
            }

            var printer = await store.FindPrinterAsync(command.PrinterId, command.BranchId, cancellationToken);
            if (printer is null)
            {
                return Result.Failure<PrintRouteResponse>(PrintingErrors.NotFound());
            }

            if (printer.Status != PrinterStatus.Active.ToString())
            {
                return Result.Failure<PrintRouteResponse>(PrintingErrors.PrinterInactive());
            }

            return await store.SetRouteAsync(
                command.BranchId,
                command.StationId,
                command.DocumentType,
                command.PrinterId,
                actorId,
                cancellationToken);
        }
    }

    internal static class PrinterCommandScope
    {
        internal static async Task<Result<T>> RunAsync<T>(
            ICurrentTenantUser currentUser,
            Guid branchId,
            Func<Guid, Task<Result<T>>> action)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<T>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != branchId)
            {
                return Result.Failure<T>(PrintingErrors.NotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<T>(AuthErrors.Forbidden());
            }

            return await action(actorId);
        }
    }
}
