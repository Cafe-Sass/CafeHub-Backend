using CafeHub.Infrastructure.Media;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Media
{
    public sealed class MediaOptionsBinderTests
    {
        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static string CreateRoot(string name)
        {
            return Path.Combine(Path.GetTempPath(), "cafehub-tests", Guid.NewGuid().ToString("N"), name);
        }

        private static InvalidMediaConfigurationException BindInvalid(
            Dictionary<string, string?> settings,
            string contentRootPath)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidMediaConfigurationException>(
                () => MediaOptionsBinder.Bind(configuration, contentRootPath));
        }

        [Fact]
        public void Bind_WithAnAbsoluteRootOutsideTheContentRoot_ReturnsTheConfiguredValues()
        {
            var contentRoot = CreateRoot("app");
            var rootPath = CreateRoot("media");
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [MediaConfigurationKeys.RootPathPath] = rootPath,
                [MediaConfigurationKeys.MaxBytesPath] = "2048",
            });

            var options = MediaOptionsBinder.Bind(configuration, contentRoot);

            Assert.Equal(rootPath, options.RootPath);
            Assert.Equal(2048, options.MaxBytes);
        }

        [Fact]
        public void Bind_WithoutMaxBytes_UsesTheFiveMegabyteDefault()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [MediaConfigurationKeys.RootPathPath] = CreateRoot("media"),
            });

            var options = MediaOptionsBinder.Bind(configuration, CreateRoot("app"));

            Assert.Equal(5242880, options.MaxBytes);
        }

        [Fact]
        public void Bind_WithoutARootPath_Fails()
        {
            var exception = BindInvalid(new Dictionary<string, string?>(), CreateRoot("app"));

            Assert.Contains(exception.Problems, problem => problem.Contains("Media:RootPath is required", StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithARelativeRootPath_Fails()
        {
            var exception = BindInvalid(
                new Dictionary<string, string?> { [MediaConfigurationKeys.RootPathPath] = "media/uploads" },
                CreateRoot("app"));

            Assert.Contains(exception.Problems, problem => problem.Contains("must be an absolute path", StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithARootPathInsideTheContentRoot_Fails()
        {
            var contentRoot = CreateRoot("app");

            var exception = BindInvalid(
                new Dictionary<string, string?> { [MediaConfigurationKeys.RootPathPath] = Path.Combine(contentRoot, "media") },
                contentRoot);

            Assert.Contains(exception.Problems, problem => problem.Contains("outside the application content root", StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithTheContentRootItself_Fails()
        {
            var contentRoot = CreateRoot("app");

            var exception = BindInvalid(
                new Dictionary<string, string?> { [MediaConfigurationKeys.RootPathPath] = contentRoot },
                contentRoot);

            Assert.Contains(exception.Problems, problem => problem.Contains("outside the application content root", StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithARootPathInsideAWebServedFolder_Fails()
        {
            var exception = BindInvalid(
                new Dictionary<string, string?>
                {
                    [MediaConfigurationKeys.RootPathPath] = Path.Combine(CreateRoot("site"), "wwwroot", "media"),
                },
                CreateRoot("app"));

            Assert.Contains(exception.Problems, problem => problem.Contains("web-served wwwroot", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("1023")]
        [InlineData("20971521")]
        [InlineData("-1")]
        public void Bind_WithAnOutOfRangeMaxBytes_Fails(string maxBytes)
        {
            var exception = BindInvalid(
                new Dictionary<string, string?>
                {
                    [MediaConfigurationKeys.RootPathPath] = CreateRoot("media"),
                    [MediaConfigurationKeys.MaxBytesPath] = maxBytes,
                },
                CreateRoot("app"));

            Assert.Contains(exception.Problems, problem => problem.Contains("Media:MaxBytes must be between", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("1024")]
        [InlineData("20971520")]
        public void Bind_WithTheRangeBoundaries_Succeeds(string maxBytes)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [MediaConfigurationKeys.RootPathPath] = CreateRoot("media"),
                [MediaConfigurationKeys.MaxBytesPath] = maxBytes,
            });

            var options = MediaOptionsBinder.Bind(configuration, CreateRoot("app"));

            Assert.Equal(int.Parse(maxBytes), options.MaxBytes);
        }

        [Fact]
        public void Bind_WithANonNumericMaxBytes_Fails()
        {
            var exception = BindInvalid(
                new Dictionary<string, string?>
                {
                    [MediaConfigurationKeys.RootPathPath] = CreateRoot("media"),
                    [MediaConfigurationKeys.MaxBytesPath] = "large",
                },
                CreateRoot("app"));

            Assert.Contains(exception.Problems, problem => problem.Contains("not a valid 32-bit integer", StringComparison.Ordinal));
        }
    }
}
