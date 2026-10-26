using CafeHub.Application.Provisioning.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Provisioning
{
    internal sealed class ProvisioningWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ProvisioningWorkerOptions _options;
        private readonly ILogger<ProvisioningWorker> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly string _workerId;

        public ProvisioningWorker(
            IServiceScopeFactory scopeFactory,
            ProvisioningWorkerOptions options,
            ILogger<ProvisioningWorker> logger,
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
                _logger.LogInformation("Provisioning worker is disabled by {ConfigurationPath}.",
                    ProvisioningWorkerConfigurationKeys.SectionName);
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
                await ProcessNextJobAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provisioning worker iteration failed.");
            }
        }

        private async Task ProcessNextJobAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var claimer = scope.ServiceProvider.GetRequiredService<IProvisioningJobClaimer>();
            var runner = scope.ServiceProvider.GetRequiredService<IProvisioningJobRunner>();
            var now = _timeProvider.GetUtcNow();

            var claimResult = await claimer.TryClaimNextAsync(
                _workerId,
                now,
                TimeSpan.FromSeconds(_options.LeaseSeconds),
                cancellationToken);

            if (claimResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to claim provisioning job: {ErrorCode}",
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provisioning job {JobId} failed during execution.", claimed.JobId);
            }
        }
    }
}
