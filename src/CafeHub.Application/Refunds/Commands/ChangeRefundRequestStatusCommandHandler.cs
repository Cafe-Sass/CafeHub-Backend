using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed class ChangeRefundRequestStatusCommandHandler(
        IRefundRequestStore store,
        ITenantAuditWriter auditWriter,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<ChangeRefundRequestStatusCommand, ChangeRefundRequestStatusResult>
    {
        private const string TargetType = "refund_requests";

        public async Task<Result<ChangeRefundRequestStatusResult>> Handle(
            ChangeRefundRequestStatusCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<ChangeRefundRequestStatusResult>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<ChangeRefundRequestStatusResult>(AuthErrors.Forbidden());
            }

            var target = Enum.Parse<RefundRequestStatus>(command.Status!, true);
            var request = await store.FindAsync(scope.Value, command.RefundRequestId, cancellationToken);
            if (request is null)
            {
                return Result.Failure<ChangeRefundRequestStatusResult>(RefundRequestErrors.NotFound());
            }

            var previousStatus = request.Status;
            if (!request.TransitionTo(Guid.CreateVersion7(), target, actorId, command.Note, timeProvider.GetUtcNow()))
            {
                return Result.Failure<ChangeRefundRequestStatusResult>(
                    RefundRequestErrors.StatusConflict(request.Status));
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "refund_requests.status_changed.succeeded",
                TargetType = TargetType,
                TargetId = request.Id,
                ActorUserId = actorId,
                Changes = TenantAuditChanges.Update(
                [
                    new TenantAuditChanges.FieldChange(
                        "status",
                        previousStatus.ToString(),
                        request.Status.ToString()),
                ]),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<ChangeRefundRequestStatusResult>(auditResult.Error!);
            }

            var saved = await store.SaveAsync(request, cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<ChangeRefundRequestStatusResult>(saved.Error!);
            }

            return Result.Success(new ChangeRefundRequestStatusResult(
                request.Id,
                request.Status.ToString(),
                request.ResolutionNote,
                request.DecidedAt));
        }
    }
}
