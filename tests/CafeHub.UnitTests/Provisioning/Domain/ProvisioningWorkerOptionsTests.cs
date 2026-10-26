using CafeHub.Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Provisioning.Domain
{
    public sealed class ProvisioningWorkerOptionsTests
    {
        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            var prefixed = settings.ToDictionary(
                pair => $"{ProvisioningWorkerConfigurationKeys.SectionName}:{pair.Key}",
                pair => pair.Value);

            return new ConfigurationBuilder()
                .AddInMemoryCollection(prefixed)
                .Build();
        }

        [Fact]
        public void Validate_AcceptsDefaults()
        {
            var options = new ProvisioningWorkerOptions();

            var problems = options.Validate();

            Assert.Empty(problems);
        }

        [Fact]
        public void Validate_AcceptsValidCustomValues()
        {
            var options = new ProvisioningWorkerOptions
            {
                Enabled = true,
                PollIntervalSeconds = 10,
                LeaseSeconds = 120,
                WorkerId = "worker-1",
            };

            var problems = options.Validate();

            Assert.Empty(problems);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(301)]
        [InlineData(-1)]
        public void Validate_RejectsPollIntervalOutsideRange(int pollIntervalSeconds)
        {
            var options = new ProvisioningWorkerOptions { PollIntervalSeconds = pollIntervalSeconds };

            var problems = options.Validate();

            Assert.Contains(problems, p => p.Contains("PollIntervalSeconds", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(29)]
        [InlineData(3601)]
        [InlineData(-1)]
        public void Validate_RejectsLeaseSecondsOutsideRange(int leaseSeconds)
        {
            var options = new ProvisioningWorkerOptions { LeaseSeconds = leaseSeconds };

            var problems = options.Validate();

            Assert.Contains(problems, p => p.Contains("LeaseSeconds", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_RejectsLeaseNotGreaterThanPoll()
        {
            var options = new ProvisioningWorkerOptions { PollIntervalSeconds = 60, LeaseSeconds = 60 };

            var problems = options.Validate();

            Assert.Contains(problems, p => p.Contains("LeaseSeconds", StringComparison.Ordinal) && p.Contains("PollIntervalSeconds", StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithValidSettings_ReturnsBoundValues()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [ProvisioningWorkerConfigurationKeys.EnabledKey] = "true",
                [ProvisioningWorkerConfigurationKeys.PollIntervalSecondsKey] = "10",
                [ProvisioningWorkerConfigurationKeys.LeaseSecondsKey] = "120",
                [ProvisioningWorkerConfigurationKeys.WorkerIdKey] = "worker-1",
            });

            var options = ProvisioningWorkerOptionsBinder.Bind(configuration);

            Assert.True(options.Enabled);
            Assert.Equal(10, options.PollIntervalSeconds);
            Assert.Equal(120, options.LeaseSeconds);
            Assert.Equal("worker-1", options.WorkerId);
        }

        [Fact]
        public void Bind_WithMissingSection_AppliesDefaults()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = ProvisioningWorkerOptionsBinder.Bind(configuration);

            Assert.False(options.Enabled);
            Assert.Equal(5, options.PollIntervalSeconds);
            Assert.Equal(300, options.LeaseSeconds);
            Assert.False(string.IsNullOrWhiteSpace(options.WorkerId));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("301")]
        public void Bind_WithInvalidPollInterval_Throws(string pollIntervalSeconds)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [ProvisioningWorkerConfigurationKeys.PollIntervalSecondsKey] = pollIntervalSeconds,
            });

            Assert.Throws<InvalidOperationException>(() => ProvisioningWorkerOptionsBinder.Bind(configuration));
        }

        [Theory]
        [InlineData("29")]
        [InlineData("3601")]
        public void Bind_WithInvalidLeaseSeconds_Throws(string leaseSeconds)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [ProvisioningWorkerConfigurationKeys.LeaseSecondsKey] = leaseSeconds,
            });

            Assert.Throws<InvalidOperationException>(() => ProvisioningWorkerOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithLeaseNotGreaterThanPoll_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [ProvisioningWorkerConfigurationKeys.PollIntervalSecondsKey] = "60",
                [ProvisioningWorkerConfigurationKeys.LeaseSecondsKey] = "60",
            });

            Assert.Throws<InvalidOperationException>(() => ProvisioningWorkerOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithNonIntegerPollInterval_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [ProvisioningWorkerConfigurationKeys.PollIntervalSecondsKey] = "ten",
            });

            Assert.Throws<InvalidOperationException>(() => ProvisioningWorkerOptionsBinder.Bind(configuration));
        }
    }
}
