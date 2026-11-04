using CafeHub.Application.Upgrades.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Upgrades
{
    internal sealed class TenantSchemaUpgradeWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TenantSchemaUpgradeWorkerOptions _options;
        private readonly ILogger<TenantSchemaUpgradeWorker> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly string _workerId;

        public TenantSchemaUpgradeWorker(
            IServiceScopeFactory scopeFactory,
            TenantSchemaUpgradeWorkerOptions options,
            ILogger<TenantSchemaUpgradeWorker> logger,
            TimeProvider timeProvider)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _workerId = string.IsNullOrWhiteSpace(options.WorkerId)
                ? $"{Environment.MachineName}-{Guid.CreateVersion7():N}"
                : options.WorkerId;
        }

        public async Task ExecuteOnceAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RunIterationAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation(
                    "Tenant schema upgrade worker is disabled by {ConfigurationPath}.",
                    TenantSchemaUpgradeWorkerConfigurationKeys.SectionName);
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunIterationAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RunIterationAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ProcessNextRunAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Tenant schema upgrade worker iteration failed ({ExceptionType}).",
                    exception.GetType().Name);
            }
        }

        private async Task ProcessNextRunAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var claimer = scope.ServiceProvider.GetRequiredService<ITenantSchemaUpgradeRunClaimer>();
            var runner = scope.ServiceProvider.GetRequiredService<ITenantSchemaUpgradeRunner>();
            var now = _timeProvider.GetUtcNow();

            var claimResult = await claimer.TryClaimNextAsync(
                _workerId,
                now,
                TimeSpan.FromSeconds(_options.LeaseSeconds),
                cancellationToken);

            if (claimResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to claim tenant schema upgrade run: {ErrorCode}",
                    claimResult.Error!.Code);
                return;
            }

            var claimed = claimResult.Value;
            if (claimed is null)
            {
                return;
            }

            try
            {
                await runner.RunAsync(claimed, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Tenant schema upgrade run {RunId} failed during execution ({ExceptionType}).",
                    claimed.RunId,
                    exception.GetType().Name);
            }
        }
    }
}
