using CafeHub.Application.Abstractions.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CafeHub.WebApi.HealthChecks
{
    public sealed class MasterDatabaseHealthCheck : IHealthCheck
    {
        private const int TimeoutSeconds = 3;
        private readonly IMasterDatabaseProbe probe;

        public MasterDatabaseHealthCheck(IMasterDatabaseProbe probe)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            try
            {
                var canConnect = await this.probe.CanConnectAsync(timeout.Token);
                return canConnect
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy();
            }
            catch (OperationCanceledException) when (timeout.Token.IsCancellationRequested)
            {
                return HealthCheckResult.Unhealthy();
            }
            catch
            {
                return HealthCheckResult.Unhealthy();
            }
        }
    }
}
