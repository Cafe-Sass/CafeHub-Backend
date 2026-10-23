namespace CafeHub.Application.Pagination
{
    public static class PaginationConfigurationKeys
    {
        public const string SectionName = "Pagination";
        public const string DefaultPageSizeKey = "DefaultPageSize";
        public const string MaxPageSizeKey = "MaxPageSize";

        public const string DefaultPageSizePath = $"{SectionName}:{DefaultPageSizeKey}";
        public const string MaxPageSizePath = $"{SectionName}:{MaxPageSizeKey}";
    }
}
