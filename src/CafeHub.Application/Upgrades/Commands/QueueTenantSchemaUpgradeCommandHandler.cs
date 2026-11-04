using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Upgrades.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Upgrades.Commands
{
    public sealed class QueueTenantSchemaUpgradeCommandHandler
        : ICommandHandler<QueueTenantSchemaUpgradeCommand, TenantSchemaUpgradeResponse>
    {
        private readonly TenantSchemaUpgradeService upgradeService;
        private readonly ICurrentUser currentUser;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<QueueTenantSchemaUpgradeCommandHandler> logger;

        public QueueTenantSchemaUpgradeCommandHandler(
            TenantSchemaUpgradeService upgradeService,
            ICurrentUser currentUser,
            TimeProvider timeProvider,
            ILogger<QueueTenantSchemaUpgradeCommandHandler> logger)
        {
            this.upgradeService = upgradeService ?? throw new ArgumentNullException(nameof(upgradeService));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<TenantSchemaUpgradeResponse>> Handle(
            QueueTenantSchemaUpgradeCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var requestedBy = currentUser.AdminId
                ?? throw new InvalidOperationException("The queueing administrator is required.");

            var tenantId = command.TenantKey;

            var queueResult = await upgradeService.QueueAsync(
                tenantId,
                requestedBy,
                cancellationToken);

            if (queueResult.IsFailure)
            {
                return Result.Failure<TenantSchemaUpgradeResponse>(queueResult.Error!);
            }

            var run = queueResult.Value!;

            logger.LogInformation(
                "Queued tenant schema upgrade run {RunId} for tenant {TenantId}.",
                run.Id,
                run.TenantId);

            return Result.Success(new TenantSchemaUpgradeResponse(
                run.Id,
                run.TenantId,
                run.Status.ToString(),
                run.TargetMigrationId,
                run.RequestedBy,
                run.CreatedAt,
                run.CreatedBy));
        }
    }
}
