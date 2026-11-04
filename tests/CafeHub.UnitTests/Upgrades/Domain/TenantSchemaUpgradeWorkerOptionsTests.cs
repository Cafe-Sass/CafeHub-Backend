using CafeHub.Infrastructure.Upgrades;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Upgrades.Domain
{
    public sealed class TenantSchemaUpgradeWorkerOptionsTests
    {
        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            var prefixed = settings.ToDictionary(
                pair => $"{TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{pair.Key}",
                pair => pair.Value);

            return new ConfigurationBuilder()
                .AddInMemoryCollection(prefixed)
                .Build();
        }

        [Fact]
        public void Validate_AcceptsDefaults()
        {
            var options = new TenantSchemaUpgradeWorkerOptions();

            var problems = options.Validate();

            Assert.Empty(problems);
        }

        [Fact]
        public void Validate_AcceptsValidCustomValues()
        {
            var options = new TenantSchemaUpgradeWorkerOptions
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
            var options = new TenantSchemaUpgradeWorkerOptions { PollIntervalSeconds = pollIntervalSeconds };

            var problems = options.Validate();

            Assert.Contains(problems, problem =>
                problem.Contains("PollIntervalSeconds", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(29)]
        [InlineData(3601)]
        [InlineData(-1)]
        public void Validate_RejectsLeaseSecondsOutsideRange(int leaseSeconds)
        {
            var options = new TenantSchemaUpgradeWorkerOptions { LeaseSeconds = leaseSeconds };

            var problems = options.Validate();

            Assert.Contains(problems, problem =>
                problem.Contains("LeaseSeconds", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_RejectsLeaseNotGreaterThanPoll()
        {
            var options = new TenantSchemaUpgradeWorkerOptions
            {
                PollIntervalSeconds = 60,
                LeaseSeconds = 60,
            };

            var problems = options.Validate();

            Assert.Contains(problems, problem =>
                problem.Contains("LeaseSeconds", StringComparison.Ordinal) &&
                problem.Contains("PollIntervalSeconds", StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithValidSettings_ReturnsBoundValues()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [TenantSchemaUpgradeWorkerConfigurationKeys.EnabledKey] = "true",
                [TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey] = "10",
                [TenantSchemaUpgradeWorkerConfigurationKeys.LeaseSecondsKey] = "120",
                [TenantSchemaUpgradeWorkerConfigurationKeys.WorkerIdKey] = "worker-1",
            });

            var options = TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration);

            Assert.True(options.Enabled);
            Assert.Equal(10, options.PollIntervalSeconds);
            Assert.Equal(120, options.LeaseSeconds);
            Assert.Equal("worker-1", options.WorkerId);
        }

        [Fact]
        public void Bind_WithMissingSection_AppliesDefaults()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration);

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
                [TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey] = pollIntervalSeconds,
            });

            Assert.Throws<InvalidOperationException>(() =>
                TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration));
        }

        [Theory]
        [InlineData("29")]
        [InlineData("3601")]
        public void Bind_WithInvalidLeaseSeconds_Throws(string leaseSeconds)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [TenantSchemaUpgradeWorkerConfigurationKeys.LeaseSecondsKey] = leaseSeconds,
            });

            Assert.Throws<InvalidOperationException>(() =>
                TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithLeaseNotGreaterThanPoll_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey] = "60",
                [TenantSchemaUpgradeWorkerConfigurationKeys.LeaseSecondsKey] = "60",
            });

            Assert.Throws<InvalidOperationException>(() =>
                TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithNonIntegerPollInterval_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey] = "ten",
            });

            Assert.Throws<InvalidOperationException>(() =>
                TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithNonBooleanEnabled_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [TenantSchemaUpgradeWorkerConfigurationKeys.EnabledKey] = "yes",
            });

            Assert.Throws<InvalidOperationException>(() =>
                TenantSchemaUpgradeWorkerOptionsBinder.Bind(configuration));
        }
    }
}
