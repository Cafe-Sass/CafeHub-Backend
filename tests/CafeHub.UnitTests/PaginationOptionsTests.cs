using CafeHub.Application;
using CafeHub.Application.Pagination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class PaginationOptionsTests
    {
        [Fact]
        public void Bind_WithoutConfiguration_AppliesTheDocumentedDefaults()
        {
            var options = PaginationOptionsBinder.Bind(null);

            Assert.Equal(20, options.DefaultPageSize);
            Assert.Equal(100, options.MaxPageSize);
        }

        [Fact]
        public void Bind_WithMissingPaginationSection_AppliesTheDocumentedDefaults()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = PaginationOptionsBinder.Bind(configuration);

            Assert.Equal(20, options.DefaultPageSize);
            Assert.Equal(100, options.MaxPageSize);
        }

        [Fact]
        public void Bind_WithConfiguredSizes_ReturnsTheBoundValues()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                ["Pagination:DefaultPageSize"] = "25",
                ["Pagination:MaxPageSize"] = "50",
            });

            var options = PaginationOptionsBinder.Bind(configuration);

            Assert.Equal(25, options.DefaultPageSize);
            Assert.Equal(50, options.MaxPageSize);
        }

        [Fact]
        public void Bind_PreservesTheDefault_WhenOnlyTheMaxPageSizeKeyIsPresent()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?> { ["Pagination:MaxPageSize"] = "50" });

            var options = PaginationOptionsBinder.Bind(configuration);

            Assert.Equal(20, options.DefaultPageSize);
            Assert.Equal(50, options.MaxPageSize);
        }

        [Theory]
        [InlineData("Pagination:DefaultPageSize", "0")]
        [InlineData("Pagination:DefaultPageSize", "-1")]
        [InlineData("Pagination:MaxPageSize", "0")]
        [InlineData("Pagination:MaxPageSize", "-5")]
        [InlineData("Pagination:DefaultPageSize", "150")]
        [InlineData("Pagination:DefaultPageSize", "abc")]
        [InlineData("Pagination:MaxPageSize", "1,5")]
        public void Bind_WithInvalidValues_ThrowsAtStartup(
            string key,
            string invalidValue)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?> { [key] = invalidValue });

            var exception = Record.Exception(() => PaginationOptionsBinder.Bind(configuration));

            Assert.IsType<InvalidPaginationConfigurationException>(exception);
            Assert.Contains($"{PaginationConfigurationKeys.SectionName} options configuration is invalid", exception!.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithDisorderedSizes_NamesBothSettingPathsInTheException()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                ["Pagination:DefaultPageSize"] = "40",
                ["Pagination:MaxPageSize"] = "10",
            });

            var exception = Assert.Throws<InvalidPaginationConfigurationException>(
                () => PaginationOptionsBinder.Bind(configuration));

            Assert.Contains(PaginationConfigurationKeys.DefaultPageSizePath, exception.Message, StringComparison.Ordinal);
            Assert.Contains(PaginationConfigurationKeys.MaxPageSizePath, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolvePageSize_MissingRequestValue_ReturnsTheDefaultPageSize()
        {
            var options = new PaginationOptions();

            Assert.Equal(20, options.ResolvePageSize(null));
        }

        [Fact]
        public void ResolvePageSize_RequestedValue_ReturnsTheRequestedPageSize()
        {
            var options = new PaginationOptions();

            Assert.Equal(50, options.ResolvePageSize(50));
        }

        [Fact]
        public void ResolvePageSize_RequestAboveTheMaximum_ThrowsInsteadOfClamping()
        {
            var options = new PaginationOptions { MaxPageSize = 100 };

            Assert.Throws<InvalidOperationException>(() => options.ResolvePageSize(101));
        }

        [Fact]
        public void AddApplication_RegistersTheBoundPaginationOptions()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                ["Pagination:DefaultPageSize"] = "25",
                ["Pagination:MaxPageSize"] = "60",
            });

            using var provider = new ServiceCollection()
                .AddApplication(configuration)
                .BuildServiceProvider();

            var options = provider.GetRequiredService<PaginationOptions>();

            Assert.Equal(25, options.DefaultPageSize);
            Assert.Equal(60, options.MaxPageSize);
        }

        [Fact]
        public void AddApplication_WithInvalidValues_ThrowsBeforeAnyRegistration()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?> { ["Pagination:DefaultPageSize"] = "0" });

            var exception = Record.Exception(() => new ServiceCollection().AddApplication(configuration));

            Assert.IsType<InvalidPaginationConfigurationException>(exception);
        }

        private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
