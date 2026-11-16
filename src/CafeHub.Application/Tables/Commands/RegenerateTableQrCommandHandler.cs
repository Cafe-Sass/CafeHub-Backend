using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class RegenerateTableQrCommandHandler(
        ITableStore store,
        ICurrentTenantUser currentTenantUser,
        ITenantContextAccessor tenantContextAccessor) : ICommandHandler<RegenerateTableQrCommand, TableQrData>
    {
        public async Task<Result<TableQrData>> Handle(RegenerateTableQrCommand command, CancellationToken cancellationToken)
        {
            if (!TableActor.TryGetActorUserId(currentTenantUser, tenantContextAccessor.GetRequired(), out var actorId))
            {
                return Result.Failure<TableQrData>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var scope = TableActor.EnsureBranchScope(currentTenantUser, command.BranchId);
            if (scope.IsFailure)
            {
                return Result.Failure<TableQrData>(scope.Error!);
            }

            return await store.RegenerateQrAsync(
                command.BranchId, command.TableId, command.ConcurrencyToken, actorId, cancellationToken);
        }
    }
}
