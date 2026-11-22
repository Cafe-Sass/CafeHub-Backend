using System.Data;
using CafeHub.Infrastructure.Orders;

namespace CafeHub.UnitTests.Orders
{
    public sealed class CafeBusinessDateProviderTests
    {
        [Theory]
        [InlineData("Africa/Cairo", "2026-11-22T22:30:00+00:00", "2026-11-23")]
        [InlineData("Africa/Cairo", "2026-11-22T21:30:00+00:00", "2026-11-22")]
        [InlineData("UTC", "2026-11-22T22:30:00+00:00", "2026-11-22")]
        public async Task TheBusinessDateIsTheCafeLocalDate(
            string timezone, string instant, string expected)
        {
            var table = new DataTable();
            table.Columns.Add("timezone", typeof(string));
            table.Rows.Add(timezone);
            var stub = new TenantCommandStub((_, _) => table);
            await using var context = stub.CreateContext();
            var provider = new CafeBusinessDateProvider(
                context, new FixedClock(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture)));

            var date = await provider.GetBusinessDateAsync(default);

            Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), date);
        }

        [Fact]
        public async Task ABlankCafeTimeZoneFailsClosed()
        {
            var table = new DataTable();
            table.Columns.Add("timezone", typeof(string));
            table.Rows.Add(string.Empty);
            var stub = new TenantCommandStub((_, _) => table);
            await using var context = stub.CreateContext();
            var provider = new CafeBusinessDateProvider(
                context, new FixedClock(DateTimeOffset.Parse("2026-11-22T22:30:00+00:00", System.Globalization.CultureInfo.InvariantCulture)));

            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetBusinessDateAsync(default));
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
