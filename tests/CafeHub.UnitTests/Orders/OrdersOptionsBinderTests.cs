using CafeHub.Application.Orders;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrdersOptionsBinderTests
    {
        [Fact]
        public void AMissingSectionFallsBackToTheDocumentedDefault()
        {
            var options = OrdersOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(300, options.ResponseWindowSeconds);
        }

        [Fact]
        public void AConfiguredWindowIsRead()
        {
            var options = OrdersOptionsBinder.Bind(Configuration("120"));

            Assert.Equal(120, options.ResponseWindowSeconds);
        }

        [Theory]
        [InlineData("29")]
        [InlineData("3601")]
        [InlineData("not-a-number")]
        public void AnOutOfRangeOrUnparsableWindowFailsClosed(string value)
        {
            var exception = Assert.Throws<InvalidOrdersConfigurationException>(
                () => OrdersOptionsBinder.Bind(Configuration(value)));

            Assert.NotEmpty(exception.Problems);
        }

        [Fact]
        public void TheWindowBoundariesAreAccepted()
        {
            Assert.Equal(30, OrdersOptionsBinder.Bind(Configuration("30")).ResponseWindowSeconds);
            Assert.Equal(3600, OrdersOptionsBinder.Bind(Configuration("3600")).ResponseWindowSeconds);
        }

        private static IConfiguration Configuration(string value) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Orders:ResponseWindowSeconds"] = value,
                })
                .Build();
    }
}
