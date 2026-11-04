using CafeHub.Application.Abstractions.Persistence;
using CafeHub.WebApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CafeHub.UnitTests
{
    public sealed class MasterDatabaseHealthCheckTests
    {
        private sealed class FakeProbe : IMasterDatabaseProbe
        {
            public Func<CancellationToken, Task<bool>> CanConnectAsyncFunc { get; set; } = _ => Task.FromResult(false);

            public int CanConnectAsyncCalls { get; private set; }

            public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
            {
                this.CanConnectAsyncCalls++;
                return this.CanConnectAsyncFunc(cancellationToken);
            }
        }

        [Fact]
        public async Task ProbeReturnsTrue_ReturnsHealthyResultWithoutDescriptionOrData()
        {
            var probe = new FakeProbe { CanConnectAsyncFunc = _ => Task.FromResult(true) };
            var check = new MasterDatabaseHealthCheck(probe);

            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Null(result.Description);
            Assert.Empty(result.Data);
            Assert.Null(result.Exception);
            Assert.Equal(1, probe.CanConnectAsyncCalls);
        }

        [Fact]
        public async Task ProbeReturnsFalse_ReturnsUnhealthyResultWithoutDescriptionOrData()
        {
            var probe = new FakeProbe { CanConnectAsyncFunc = _ => Task.FromResult(false) };
            var check = new MasterDatabaseHealthCheck(probe);

            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Null(result.Description);
            Assert.Empty(result.Data);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task ProbeThrowsConnectionStringMessage_ReturnsUnhealthyWithoutTheMessage()
        {
            var probe = new FakeProbe
            {
                CanConnectAsyncFunc = _ => throw new InvalidOperationException("Server=127.0.0.1;Port=5432;Password=secret;Database=master;")
            };
            var check = new MasterDatabaseHealthCheck(probe);

            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Null(result.Description);
            Assert.Empty(result.Data);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task ProbeThatExceedsTimeout_ReturnsUnhealthy()
        {
            using var testAbort = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var probe = new FakeProbe
            {
                CanConnectAsyncFunc = async token =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    return true;
                }
            };
            var check = new MasterDatabaseHealthCheck(probe);

            var result = await check.CheckHealthAsync(new HealthCheckContext(), testAbort.Token);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Null(result.Description);
        }
    }
}
