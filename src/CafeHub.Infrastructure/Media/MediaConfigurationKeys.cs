namespace CafeHub.Infrastructure.Media
{
    public static class MediaConfigurationKeys
    {
        public const string SectionName = "Media";
        public const string RootPathKey = "RootPath";
        public const string MaxBytesKey = "MaxBytes";

        public const string RootPathPath = SectionName + ":" + RootPathKey;
        public const string MaxBytesPath = SectionName + ":" + MaxBytesKey;
    }
}
