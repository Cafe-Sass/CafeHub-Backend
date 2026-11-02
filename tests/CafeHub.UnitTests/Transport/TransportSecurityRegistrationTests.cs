using System.Net;
using CafeHub.WebApi.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests.Transport
{
    public sealed class TransportSecurityRegistrationTests
    {
        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddTransportSecurity(CreateConfiguration(settings));

            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddTransportSecurity_RegistersTheBoundOptions()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>
            {
                [TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath] = "30",
                [TransportSecurityConfigurationKeys.HttpsPortPath] = "8443",
            });

            var options = serviceProvider.GetRequiredService<TransportSecurityOptions>();

            Assert.Equal(30, options.Hsts.MaxAgeDays);
            Assert.Equal(8443, options.HttpsPort);
        }

        [Fact]
        public void AddTransportSecurity_ConfiguresHstsFromTheBoundOptions()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>
            {
                [TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath] = "30",
                [TransportSecurityConfigurationKeys.HstsIncludeSubDomainsPath] = "true",
                [TransportSecurityConfigurationKeys.HstsPreloadPath] = "true",
            });

            var hstsOptions = serviceProvider.GetRequiredService<IOptions<HstsOptions>>().Value;

            Assert.Equal(TimeSpan.FromDays(30), hstsOptions.MaxAge);
            Assert.True(hstsOptions.IncludeSubDomains);
            Assert.True(hstsOptions.Preload);
        }

        [Fact]
        public void AddTransportSecurity_ConfiguresTheHttpsPort()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>
            {
                [TransportSecurityConfigurationKeys.HttpsPortPath] = "8443",
            });

            var redirectionOptions = serviceProvider.GetRequiredService<IOptions<HttpsRedirectionOptions>>().Value;

            Assert.Equal(8443, redirectionOptions.HttpsPort);
        }

        [Fact]
        public void AddTransportSecurity_WithForwardingDisabled_TrustsNoSourceAndProcessesNoForwardedHeader()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>());

            var forwardedHeadersOptions = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

            Assert.Equal(ForwardedHeaders.None, forwardedHeadersOptions.ForwardedHeaders);
            Assert.Empty(forwardedHeadersOptions.KnownProxies);
            Assert.Empty(forwardedHeadersOptions.KnownIPNetworks);
        }

        [Fact]
        public void AddTransportSecurity_WithForwardingEnabled_ContainsOnlyTheConfiguredProxiesNetworksAndLimit()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>
            {
                [TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "true",
                [$"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownProxiesPath}:0"] = "10.1.2.3",
                [$"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownNetworksPath}:0"] = "192.168.0.0/16",
                [TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath] = "3",
            });

            var forwardedHeadersOptions = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

            Assert.Equal(
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                forwardedHeadersOptions.ForwardedHeaders);
            Assert.Equal(3, forwardedHeadersOptions.ForwardLimit);
            Assert.Equal(IPAddress.Parse("10.1.2.3"), Assert.Single(forwardedHeadersOptions.KnownProxies));
            var network = Assert.Single(forwardedHeadersOptions.KnownIPNetworks);
            Assert.Equal(IPAddress.Parse("192.168.0.0"), network.BaseAddress);
            Assert.Equal(16, network.PrefixLength);
        }

        [Fact]
        public void AddTransportSecurity_WithInvalidConfiguration_FailsAtRegistration()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "true",
            });

            var exception = Assert.Throws<InvalidTransportSecurityConfigurationException>(
                () => services.AddTransportSecurity(configuration));

            Assert.Contains(TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AddTransportSecurity_WithNullArguments_Throws()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string?>());

            Assert.Throws<ArgumentNullException>(
                () => TransportSecurityExtensions.AddTransportSecurity(null!, configuration));
            Assert.Throws<ArgumentNullException>(
                () => services.AddTransportSecurity(null!));
        }

        [Fact]
        public void UseTransportSecurity_WithNullArguments_Throws()
        {
            var applicationBuilder = new ApplicationBuilderFake();
            var environment = new HostEnvironmentFake("Production");

            Assert.Throws<ArgumentNullException>(
                () => TransportSecurityExtensions.UseTransportSecurity(null!, environment));
            Assert.Throws<ArgumentNullException>(
                () => applicationBuilder.UseTransportSecurity(null!));
        }

        private sealed class ApplicationBuilderFake : IApplicationBuilder
        {
            public IServiceProvider ApplicationServices { get; set; } = null!;

            public IFeatureCollection ServerFeatures { get; } = new FeatureCollection();

            public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

            public IApplicationBuilder New()
            {
                return new ApplicationBuilderFake();
            }

            public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
            {
                return this;
            }

            public RequestDelegate Build()
            {
                return context => Task.CompletedTask;
            }
        }

        private sealed class HostEnvironmentFake : IHostEnvironment
        {
            public HostEnvironmentFake(string environmentName)
            {
                EnvironmentName = environmentName;
            }

            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; } = "CafeHub.UnitTests";

            public string ContentRootPath { get; set; } = string.Empty;

            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
