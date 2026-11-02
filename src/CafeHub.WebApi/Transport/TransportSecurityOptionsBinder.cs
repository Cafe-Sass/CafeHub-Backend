using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace CafeHub.WebApi.Transport
{
    /// <summary>
    /// Binds the transport security settings and fails closed. Every problem is reported without echoing any
    /// configured value, so a malformed address or limit cannot leak into startup logs. Missing settings keep
    /// the documented safe defaults, and forwarded headers stay disabled by default.
    /// </summary>
    public static class TransportSecurityOptionsBinder
    {
        public static TransportSecurityOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(TransportSecurityConfigurationKeys.SectionName);
            var hstsSection = section?.GetSection(TransportSecurityConfigurationKeys.HstsSectionKey);
            var forwardedHeadersSection = section?.GetSection(TransportSecurityConfigurationKeys.ForwardedHeadersSectionKey);

            var options = new TransportSecurityOptions();
            var problems = new List<string>();

            BindInt(
                hstsSection,
                TransportSecurityConfigurationKeys.HstsMaxAgeDaysKey,
                TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath,
                value => options.Hsts.MaxAgeDays = value,
                problems);
            BindBool(
                hstsSection,
                TransportSecurityConfigurationKeys.HstsIncludeSubDomainsKey,
                TransportSecurityConfigurationKeys.HstsIncludeSubDomainsPath,
                value => options.Hsts.IncludeSubDomains = value,
                problems);
            BindBool(
                hstsSection,
                TransportSecurityConfigurationKeys.HstsPreloadKey,
                TransportSecurityConfigurationKeys.HstsPreloadPath,
                value => options.Hsts.Preload = value,
                problems);
            BindOptionalInt(
                section,
                TransportSecurityConfigurationKeys.HttpsPortKey,
                TransportSecurityConfigurationKeys.HttpsPortPath,
                value => options.HttpsPort = value,
                problems);
            BindBool(
                forwardedHeadersSection,
                TransportSecurityConfigurationKeys.ForwardedHeadersEnabledKey,
                TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath,
                value => options.ForwardedHeaders.Enabled = value,
                problems);
            BindInt(
                forwardedHeadersSection,
                TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitKey,
                TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath,
                value => options.ForwardedHeaders.ForwardLimit = value,
                problems);
            options.ForwardedHeaders.KnownProxies = BindKnownProxies(forwardedHeadersSection, problems);
            options.ForwardedHeaders.KnownNetworks = BindKnownNetworks(forwardedHeadersSection, problems);

            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidTransportSecurityConfigurationException(problems);
            }

            return options;
        }

        private static IReadOnlyList<IPAddress> BindKnownProxies(IConfiguration? section, ICollection<string> problems)
        {
            var proxies = new List<IPAddress>();
            var proxySection = section?.GetSection(TransportSecurityConfigurationKeys.ForwardedHeadersKnownProxiesKey);
            if (proxySection is null)
            {
                return proxies;
            }

            foreach (var child in proxySection.GetChildren())
            {
                if (!IPAddress.TryParse(child.Value, out var address))
                {
                    problems.Add($"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownProxiesPath} contains an entry that is not a valid IP address.");
                    continue;
                }

                proxies.Add(address);
            }

            return proxies;
        }

        private static IReadOnlyList<IPNetwork> BindKnownNetworks(IConfiguration? section, ICollection<string> problems)
        {
            var networks = new List<IPNetwork>();
            var networkSection = section?.GetSection(TransportSecurityConfigurationKeys.ForwardedHeadersKnownNetworksKey);
            if (networkSection is null)
            {
                return networks;
            }

            foreach (var child in networkSection.GetChildren())
            {
                if (!IPNetwork.TryParse(child.Value, out var network))
                {
                    problems.Add($"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownNetworksPath} contains an entry that is not a valid CIDR network.");
                    continue;
                }

                networks.Add(network);
            }

            return networks;
        }

        private static void BindInt(
            IConfiguration? section,
            string key,
            string path,
            Action<int> assign,
            ICollection<string> problems)
        {
            var rawValue = section?[key];
            if (rawValue is null)
            {
                return;
            }

            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                problems.Add($"{path} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }

        private static void BindOptionalInt(
            IConfiguration? section,
            string key,
            string path,
            Action<int> assign,
            ICollection<string> problems)
        {
            if (section?[key] is null)
            {
                return;
            }

            BindInt(section, key, path, assign, problems);
        }

        private static void BindBool(
            IConfiguration? section,
            string key,
            string path,
            Action<bool> assign,
            ICollection<string> problems)
        {
            var rawValue = section?[key];
            if (rawValue is null)
            {
                return;
            }

            if (!bool.TryParse(rawValue, out var value))
            {
                problems.Add($"{path} is not a valid boolean.");
                return;
            }

            assign(value);
        }
    }
}
