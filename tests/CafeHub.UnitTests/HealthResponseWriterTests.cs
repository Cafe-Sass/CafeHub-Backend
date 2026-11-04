using CafeHub.WebApi.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CafeHub.UnitTests
{
    public sealed class HealthResponseWriterTests
    {
        private static HealthReport Report(HealthStatus status) => new(
            new Dictionary<string, HealthReportEntry>
            {
                ["master"] = new HealthReportEntry(status, "master", TimeSpan.Zero, null, null),
            },
            TimeSpan.Zero);

        [Fact]
        public async Task HealthyReport_WritesExactlyTheFixedHealthyBody()
        {
            var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

            await HealthResponseWriter.WriteAsync(context, Report(HealthStatus.Healthy));

            Assert.Equal("application/json", context.Response.ContentType);
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Equal("{\"status\":\"Healthy\"}", body);
        }

        [Fact]
        public async Task UnhealthyReport_WritesExactlyTheFixedUnhealthyBody()
        {
            var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

            await HealthResponseWriter.WriteAsync(context, Report(HealthStatus.Unhealthy));

            Assert.Equal("application/json", context.Response.ContentType);
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Equal("{\"status\":\"Unhealthy\"}", body);
        }
    }
}
