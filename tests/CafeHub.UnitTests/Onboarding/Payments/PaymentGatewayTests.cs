using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.PaymentGateway;
using CafeHub.Infrastructure.Onboarding.Payments;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Onboarding.Payments
{
    public sealed class MockPaymentGatewayTests
    {
        [Fact]
        public async Task ChargeAsync_ReturnsSucceeded_WhenConfiguredToSucceed()
        {
            var gateway = new MockPaymentGateway(new MockPaymentOptions { Outcome = "Succeed" });

            var result = await gateway.ChargeAsync(
                new PaymentChargeRequest(49.99m, "usd", "key-123", "ref-123"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(PaymentOutcome.Succeeded, result.Value!.Outcome);
            Assert.StartsWith("mock_", result.Value.ProviderReference);
            Assert.Null(result.Value.FailureCode);
        }

        [Fact]
        public async Task ChargeAsync_ReturnsDeclined_WhenConfiguredToDecline()
        {
            var gateway = new MockPaymentGateway(new MockPaymentOptions { Outcome = "Decline" });

            var result = await gateway.ChargeAsync(
                new PaymentChargeRequest(49.99m, "usd", "key-123", "ref-123"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(PaymentOutcome.Declined, result.Value!.Outcome);
            Assert.StartsWith("mock_", result.Value.ProviderReference);
            Assert.Equal("insufficient_funds", result.Value.FailureCode);
        }

        [Fact]
        public async Task ChargeAsync_DefaultsToSucceed_WhenOutcomeNotConfigured()
        {
            var gateway = new MockPaymentGateway(new MockPaymentOptions());

            var result = await gateway.ChargeAsync(
                new PaymentChargeRequest(10.00m, "eur", "key-456", "ref-456"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(PaymentOutcome.Succeeded, result.Value!.Outcome);
        }
    }

    public sealed class PaymentOptionsBinderTests
    {
        private static PaymentOptions Bind(IDictionary<string, string?> data, bool isDevelopment)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(data)
                .Build();

            return PaymentOptionsBinder.Bind(config, isDevelopment);
        }

        [Fact]
        public void Bind_AcceptsMockInDevelopment()
        {
            var options = Bind(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Mock",
                },
                isDevelopment: true);

            Assert.Equal("Mock", options.Provider);
            Assert.Equal("Succeed", options.Mock.Outcome);
            Assert.False(options.Mock.AllowOutsideDevelopment);
        }

        [Fact]
        public void Bind_AcceptsMockOutsideDevelopment_WhenAllowFlagIsTrue()
        {
            var options = Bind(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Mock",
                    ["Payments:Mock:AllowOutsideDevelopment"] = "true",
                },
                isDevelopment: false);

            Assert.Equal("Mock", options.Provider);
            Assert.True(options.Mock.AllowOutsideDevelopment);
        }

        [Fact]
        public void Bind_RejectsUnknownProvider()
        {
            var ex = Assert.Throws<InvalidPaymentConfigurationException>(() =>
                Bind(
                    new Dictionary<string, string?>
                    {
                        ["Payments:Provider"] = "Stripe",
                    },
                    isDevelopment: true));

            Assert.Contains(ex.Problems, p => p.Contains("Payments:Provider"));
            Assert.DoesNotContain("Stripe", ex.Message);
        }

        [Fact]
        public void Bind_RejectsMockOutsideDevelopmentWithoutAllowFlag()
        {
            var ex = Assert.Throws<InvalidPaymentConfigurationException>(() =>
                Bind(
                    new Dictionary<string, string?>
                    {
                        ["Payments:Provider"] = "Mock",
                    },
                    isDevelopment: false));

            Assert.Contains(ex.Problems, p => p.Contains("Payments:Mock:AllowOutsideDevelopment"));
            Assert.DoesNotContain("false", ex.Message);
        }

        [Fact]
        public void Bind_DefaultsProviderToMock()
        {
            var options = Bind(new Dictionary<string, string?>(), isDevelopment: true);

            Assert.Equal("Mock", options.Provider);
        }

        [Fact]
        public void Bind_DefaultsOutcomeToSucceed()
        {
            var options = Bind(
                new Dictionary<string, string?>
                {
                    ["Payments:Provider"] = "Mock",
                },
                isDevelopment: true);

            Assert.Equal("Succeed", options.Mock.Outcome);
        }
    }

    public sealed class PaymentContractTests
    {
        [Fact]
        public void PaymentChargeRequest_HasNoCardRelatedMembers()
        {
            var properties = typeof(PaymentChargeRequest).GetProperties();
            var names = properties.Select(p => p.Name).ToHashSet();

            Assert.DoesNotContain("CardNumber", names);
            Assert.DoesNotContain("Card", names);
            Assert.DoesNotContain("PAN", names);
            Assert.DoesNotContain("Cvv", names);
            Assert.DoesNotContain("Expiry", names);
        }

        [Fact]
        public void PaymentChargeResult_HasNoCardRelatedMembers()
        {
            var properties = typeof(PaymentChargeResult).GetProperties();
            var names = properties.Select(p => p.Name).ToHashSet();

            Assert.DoesNotContain("CardNumber", names);
            Assert.DoesNotContain("Card", names);
            Assert.DoesNotContain("PAN", names);
            Assert.DoesNotContain("Cvv", names);
            Assert.DoesNotContain("Expiry", names);
        }

        [Fact]
        public void PaymentChargeResult_HasOnlyExpectedMembers()
        {
            var properties = typeof(PaymentChargeResult).GetProperties();
            var names = properties.Select(p => p.Name).ToHashSet();

            Assert.Equal(new HashSet<string> { "Outcome", "ProviderReference", "FailureCode" }, names);
        }

        [Fact]
        public void PaymentChargeRequest_HasOnlyExpectedMembers()
        {
            var properties = typeof(PaymentChargeRequest).GetProperties();
            var names = properties.Select(p => p.Name).ToHashSet();

            Assert.Equal(new HashSet<string> { "Amount", "Currency", "IdempotencyKey", "Reference" }, names);
        }
    }
}
