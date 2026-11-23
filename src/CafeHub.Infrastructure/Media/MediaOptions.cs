namespace CafeHub.Infrastructure.Media
{
    public sealed class MediaOptions
    {
        public const int DefaultMaxBytes = 5242880;
        public const int MinMaxBytes = 1024;
        public const int MaxMaxBytes = 20971520;

        public string RootPath { get; set; } = string.Empty;

        public int MaxBytes { get; set; } = DefaultMaxBytes;

        public string RootDirectory => Path.GetFullPath(RootPath);

        public StringComparison PathComparison =>
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public IReadOnlyList<string> Validate(string contentRootPath)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(RootPath))
            {
                problems.Add($"{MediaConfigurationKeys.RootPathPath} is required.");
            }
            else if (!Path.IsPathFullyQualified(RootPath))
            {
                problems.Add($"{MediaConfigurationKeys.RootPathPath} must be an absolute path.");
            }
            else
            {
                var rootDirectory = Path.GetFullPath(RootPath);

                if (ContainsWebServedSegment(rootDirectory))
                {
                    problems.Add($"{MediaConfigurationKeys.RootPathPath} must not point into a web-served wwwroot folder.");
                }

                if (!string.IsNullOrWhiteSpace(contentRootPath)
                    && IsSameOrInside(rootDirectory, Path.GetFullPath(contentRootPath)))
                {
                    problems.Add($"{MediaConfigurationKeys.RootPathPath} must be outside the application content root.");
                }
            }

            if (MaxBytes < MinMaxBytes || MaxBytes > MaxMaxBytes)
            {
                problems.Add(
                    $"{MediaConfigurationKeys.MaxBytesPath} must be between {MinMaxBytes} and {MaxMaxBytes} but was {MaxBytes}.");
            }

            return problems;
        }

        private bool IsSameOrInside(string candidate, string parent)
        {
            var normalizedParent = Path.EndsInDirectorySeparator(parent)
                ? parent
                : parent + Path.DirectorySeparatorChar;

            return string.Equals(candidate, parent, PathComparison)
                || candidate.StartsWith(normalizedParent, PathComparison);
        }

        private static bool ContainsWebServedSegment(string path)
        {
            var segments = path.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            return segments.Any(segment => string.Equals(segment, "wwwroot", StringComparison.OrdinalIgnoreCase));
        }
    }
}
