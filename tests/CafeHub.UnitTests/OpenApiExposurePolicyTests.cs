using CafeHub.WebApi.Documentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CafeHub.UnitTests
{
    public sealed class OpenApiExposurePolicyTests
    {
        [Fact]
        public void IsExposed_DevelopmentEnvironment_ExposesTheDocument()
        {
            var policy = CreatePolicy("Development", new Dictionary<string, string?>());

            Assert.True(policy.IsExposed());
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Testing")]
        public void IsExposed_NonDevelopmentEnvironment_DoesNotExposeTheDocument(string environmentName)
        {
            var policy = CreatePolicy(environmentName, new Dictionary<string, string?>());

            Assert.False(policy.IsExposed());
        }

        [Fact]
        public void IsExposed_NonDevelopmentEnvironment_WithFlagTrue_ExposesTheDocument()
        {
            var policy = CreatePolicy("Production", new Dictionary<string, string?>
            {
                [OpenApiExposurePolicy.ExposeOutsideDevelopmentKey] = "true",
            });

            Assert.True(policy.IsExposed());
        }

        [Fact]
        public void IsExposed_DevelopmentEnvironment_WithFlagTrue_KeepsExposingTheDocument()
        {
            var policy = CreatePolicy("Development", new Dictionary<string, string?>
            {
                [OpenApiExposurePolicy.ExposeOutsideDevelopmentKey] = "true",
            });

            Assert.True(policy.IsExposed());
        }

        [Fact]
        public void IsExposed_MissingConfigurationKey_BehavesAsUnexposed()
        {
            var policy = new OpenApiExposurePolicy(new FakeHostEnvironment("Production"), new ConfigurationBuilder().Build());

            Assert.False(policy.IsExposed());
        }

        private static OpenApiExposurePolicy CreatePolicy(string environmentName, IReadOnlyDictionary<string, string?> settings)
        {
            return new OpenApiExposurePolicy(new FakeHostEnvironment(environmentName), CreateConfiguration(settings));
        }

        private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> settings)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        private sealed class FakeHostEnvironment : IHostEnvironment
        {
            public FakeHostEnvironment(string environmentName)
            {
                this.EnvironmentName = environmentName;
            }

            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; } = "CafeHub.UnitTests";

            public string ContentRootPath { get; set; } = string.Empty;

            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
