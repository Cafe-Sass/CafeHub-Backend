using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed class AssignRefundRequestCommandHandler(
        IRefundRequestStore store,
        ITenantAuditWriter auditWriter,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<AssignRefundRequestCommand, AssignRefundRequestResult>
    {
        private const string TargetType = "refund_requests";

        public async Task<Result<AssignRefundRequestResult>> Handle(
            AssignRefundRequestCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<AssignRefundRequestResult>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<AssignRefundRequestResult>(AuthErrors.Forbidden());
            }

            var request = await store.FindAsync(scope.Value, command.RefundRequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<AssignRefundRequestResult>(RefundRequestErrors.NotFound());
            }

            if (!await store.IsAssignableStaffAsync(request.BranchId, command.AssignedUserId, cancellationToken))
            {
                return Result.Failure<AssignRefundRequestResult>(RefundRequestErrors.AssignedStaffNotFound());
            }

            var previousAssignedUserId = request.AssignedUserId;
            request.Assign(Guid.CreateVersion7(), command.AssignedUserId, actorId, timeProvider.GetUtcNow());

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "refund_requests.assigned.succeeded",
                TargetType = TargetType,
                TargetId = request.Id,
                ActorUserId = actorId,
                Changes = TenantAuditChanges.Update(
                [
                    new TenantAuditChanges.FieldChange(
                        "assigned_user_id",
                        previousAssignedUserId?.ToString(),
                        request.AssignedUserId!.Value.ToString()),
                ]),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<AssignRefundRequestResult>(auditResult.Error!);
            }

            var saved = await store.SaveAsync(request, cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<AssignRefundRequestResult>(saved.Error!);
            }

            return Result.Success(new AssignRefundRequestResult(
                request.Id,
                request.AssignedUserId!.Value,
                request.Status.ToString()));
        }
    }
}
