namespace CafeHub.WebApi.Cors
{
    public static class CorsConfigurationKeys
    {
        public const string SectionName = "Cors";
        public const string AllowedOriginsKey = "AllowedOrigins";
        public const string AllowedMethodsKey = "AllowedMethods";
        public const string AllowedHeadersKey = "AllowedHeaders";
        public const string ExposedHeadersKey = "ExposedHeaders";
        public const string AllowCredentialsKey = "AllowCredentials";
        public const string MaxAgeSecondsKey = "MaxAgeSeconds";

        public const string AllowedOriginsPath = $"{SectionName}:{AllowedOriginsKey}";
        public const string AllowedMethodsPath = $"{SectionName}:{AllowedMethodsKey}";
        public const string AllowedHeadersPath = $"{SectionName}:{AllowedHeadersKey}";
        public const string ExposedHeadersPath = $"{SectionName}:{ExposedHeadersKey}";
        public const string AllowCredentialsPath = $"{SectionName}:{AllowCredentialsKey}";
        public const string MaxAgeSecondsPath = $"{SectionName}:{MaxAgeSecondsKey}";
    }
}
