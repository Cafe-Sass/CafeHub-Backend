using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class SubmitAssistedOrderCommandHandler(
        ISender sender,
        IOrderTableStore tableStore,
        ICurrentTenantUser currentUser)
        : ICommandHandler<SubmitAssistedOrderCommand, SubmitOrderResult>
    {
        public async Task<Result<SubmitOrderResult>> Handle(
            SubmitAssistedOrderCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<SubmitOrderResult>(scope.Error!);
            }

            if (currentUser.UserId is not { } userId || userId == Guid.Empty)
            {
                return Result.Failure<SubmitOrderResult>(AuthErrors.Forbidden());
            }

            var table = await tableStore.FindActiveTableAsync(command.TableId, cancellationToken);
            if (table is null || (scope.Value is { } branchId && branchId != table.BranchId))
            {
                return Result.Failure<SubmitOrderResult>(OrdersErrors.NotFound());
            }

            return await sender.Send(
                new SubmitOrderCommand
                {
                    IdempotencyKey = command.IdempotencyKey,
                    Code = table.QrCode,
                    ServiceType = command.ServiceType,
                    Source = OrderSource.Captain,
                    GuestDisplayName = command.GuestDisplayName,
                    GuestDeviceId = null,
                    ActingStaffUserId = userId,
                    ExpectedTotal = command.ExpectedTotal,
                    Lines = command.Lines,
                },
                cancellationToken);
        }
    }
}
