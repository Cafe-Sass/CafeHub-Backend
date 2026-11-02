namespace CafeHub.WebApi.Security
{
    public static class RequestLimitsConfigurationKeys
    {
        public const string SectionName = "RequestLimits";
        public const string MaxBodyBytesKey = "MaxBodyBytes";
        public const string MaxHeadersTotalBytesKey = "MaxHeadersTotalBytes";

        public const string MaxBodyBytesPath = $"{SectionName}:{MaxBodyBytesKey}";
        public const string MaxHeadersTotalBytesPath = $"{SectionName}:{MaxHeadersTotalBytesKey}";
    }
}
