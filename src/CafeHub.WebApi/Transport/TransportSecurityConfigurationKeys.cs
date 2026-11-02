namespace CafeHub.WebApi.Transport
{
    public static class TransportSecurityConfigurationKeys
    {
        public const string SectionName = "Transport";
        public const string HstsSectionKey = "Hsts";
        public const string HstsMaxAgeDaysKey = "MaxAgeDays";
        public const string HstsIncludeSubDomainsKey = "IncludeSubDomains";
        public const string HstsPreloadKey = "Preload";
        public const string HttpsPortKey = "HttpsPort";
        public const string ForwardedHeadersSectionKey = "ForwardedHeaders";
        public const string ForwardedHeadersEnabledKey = "Enabled";
        public const string ForwardedHeadersKnownProxiesKey = "KnownProxies";
        public const string ForwardedHeadersKnownNetworksKey = "KnownNetworks";
        public const string ForwardedHeadersForwardLimitKey = "ForwardLimit";

        public const string HstsMaxAgeDaysPath = $"{SectionName}:{HstsSectionKey}:{HstsMaxAgeDaysKey}";
        public const string HstsIncludeSubDomainsPath = $"{SectionName}:{HstsSectionKey}:{HstsIncludeSubDomainsKey}";
        public const string HstsPreloadPath = $"{SectionName}:{HstsSectionKey}:{HstsPreloadKey}";
        public const string HttpsPortPath = $"{SectionName}:{HttpsPortKey}";
        public const string ForwardedHeadersEnabledPath = $"{SectionName}:{ForwardedHeadersSectionKey}:{ForwardedHeadersEnabledKey}";
        public const string ForwardedHeadersKnownProxiesPath = $"{SectionName}:{ForwardedHeadersSectionKey}:{ForwardedHeadersKnownProxiesKey}";
        public const string ForwardedHeadersKnownNetworksPath = $"{SectionName}:{ForwardedHeadersSectionKey}:{ForwardedHeadersKnownNetworksKey}";
        public const string ForwardedHeadersForwardLimitPath = $"{SectionName}:{ForwardedHeadersSectionKey}:{ForwardedHeadersForwardLimitKey}";
    }
}
