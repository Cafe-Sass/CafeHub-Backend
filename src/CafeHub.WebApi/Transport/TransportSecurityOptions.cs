using System.Net;

namespace CafeHub.WebApi.Transport
{
    /// <summary>
    /// Transport security settings. Outside Development their values drive HTTPS redirection, HSTS, and
    /// trusted forwarded headers. The forwarded headers middleware trusts only the configured proxies and
    /// networks; an enabled configuration with an empty trust list is rejected because it would silently
    /// trust nothing while appearing to forward the client address.
    /// </summary>
    public sealed class TransportSecurityOptions
    {
        public const int DefaultHstsMaxAgeDays = 365;
        public const int MinHstsMaxAgeDays = 1;
        public const int MaxHstsMaxAgeDays = 730;
        public const int MinHttpsPort = 1;
        public const int MaxHttpsPort = 65535;
        public const int DefaultForwardLimit = 1;
        public const int MinForwardLimit = 1;
        public const int MaxForwardLimit = 5;

        public TransportHstsOptions Hsts { get; set; } = new();

        public int? HttpsPort { get; set; }

        public TransportForwardedHeadersOptions ForwardedHeaders { get; set; } = new();

        public TimeSpan HstsMaxAge => TimeSpan.FromDays(Hsts.MaxAgeDays);

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (Hsts.MaxAgeDays < MinHstsMaxAgeDays || Hsts.MaxAgeDays > MaxHstsMaxAgeDays)
            {
                problems.Add(
                    $"{TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath} must be between {MinHstsMaxAgeDays} and {MaxHstsMaxAgeDays}.");
            }

            if (HttpsPort is int httpsPort && (httpsPort < MinHttpsPort || httpsPort > MaxHttpsPort))
            {
                problems.Add(
                    $"{TransportSecurityConfigurationKeys.HttpsPortPath} must be between {MinHttpsPort} and {MaxHttpsPort}.");
            }

            if (ForwardedHeaders.ForwardLimit < MinForwardLimit || ForwardedHeaders.ForwardLimit > MaxForwardLimit)
            {
                problems.Add(
                    $"{TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath} must be between {MinForwardLimit} and {MaxForwardLimit}.");
            }

            if (ForwardedHeaders.Enabled && ForwardedHeaders.KnownProxies.Count == 0 && ForwardedHeaders.KnownNetworks.Count == 0)
            {
                problems.Add(
                    $"{TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath} requires at least one known proxy or known network.");
            }

            return problems;
        }
    }

    public sealed class TransportHstsOptions
    {
        public int MaxAgeDays { get; set; } = TransportSecurityOptions.DefaultHstsMaxAgeDays;

        public bool IncludeSubDomains { get; set; }

        public bool Preload { get; set; }
    }

    public sealed class TransportForwardedHeadersOptions
    {
        public bool Enabled { get; set; }

        public int ForwardLimit { get; set; } = TransportSecurityOptions.DefaultForwardLimit;

        public IReadOnlyList<IPAddress> KnownProxies { get; set; } = Array.Empty<IPAddress>();

        public IReadOnlyList<IPNetwork> KnownNetworks { get; set; } = Array.Empty<IPNetwork>();
    }
}
