using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Persistence.Master;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests;

public sealed class MasterConnectionStringResolverTests
{
    private static MasterConnectionStringResolver CreateResolver(string? settingValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationKeys.MasterConnectionString] = settingValue
            })
            .Build();

        return new MasterConnectionStringResolver(configuration);
    }

    [Fact]
    public void Resolve_Throws_WhenSettingKeyIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var resolver = new MasterConnectionStringResolver(configuration);

        var exception = Record.Exception(() => resolver.Resolve());

        Assert.IsType<MasterDatabaseNotConfiguredException>(exception);
    }

    [Fact]
    public void Resolve_Throws_WhenSettingValueIsBlank()
    {
        var resolver = CreateResolver(" ");

        var exception = Assert.Throws<MasterDatabaseNotConfiguredException>(() => resolver.Resolve());

        Assert.Contains(ConfigurationKeys.MasterConnectionString, exception.Message);
    }

    [Fact]
    public void Resolve_ReturnsConfiguredValue()
    {
        const string configuredValue = "Host=localhost;Port=5432;Database=tests";

        var connectionString = CreateResolver(configuredValue).Resolve();

        Assert.Equal(configuredValue, connectionString);
    }

    [Fact]
    public void Resolve_ErrorMessage_NamesKey_WaysToSetIt_and_ExcludesAnyConfiguredValue()
    {
        var missingResolver = new MasterConnectionStringResolver(new ConfigurationBuilder().Build());

        var missingException = Assert.Throws<MasterDatabaseNotConfiguredException>(() => missingResolver.Resolve());

        Assert.Contains(ConfigurationKeys.MasterConnectionString, missingException.Message);
        Assert.Contains(ConfigurationKeys.MasterConnectionStringEnvironmentVariable, missingException.Message);
        Assert.Contains("User Secrets", missingException.Message);
        Assert.DoesNotContain("=", missingException.Message, StringComparison.Ordinal);

        var blankException = Assert.Throws<MasterDatabaseNotConfiguredException>(() => CreateResolver("\t   \n").Resolve());

        Assert.Equal(missingException.Message, blankException.Message);
    }
}
