namespace CafeHub.WebApi.Security
{
    /// <summary>
    /// Configurable request-size limits. A request over the body limit fails with a 413 ProblemDetails
    /// through the existing exception mapper without revealing any limit value. Header limits are applied
    /// once in Kestrel configuration.
    /// </summary>
    public sealed class RequestLimitsOptions
    {
        public const int DefaultMaxBodyBytes = 1048576;
        public const int MinMaxBodyBytes = 1024;
        public const int MaxMaxBodyBytes = 33554432;
        public const int DefaultMaxHeadersTotalBytes = 32768;
        public const int MinMaxHeadersTotalBytes = 4096;
        public const int MaxMaxHeadersTotalBytes = 131072;

        public int MaxBodyBytes { get; set; } = DefaultMaxBodyBytes;

        public int MaxHeadersTotalBytes { get; set; } = DefaultMaxHeadersTotalBytes;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (MaxBodyBytes < MinMaxBodyBytes || MaxBodyBytes > MaxMaxBodyBytes)
            {
                problems.Add(
                    $"{RequestLimitsConfigurationKeys.MaxBodyBytesPath} must be between {MinMaxBodyBytes} and {MaxMaxBodyBytes}.");
            }

            if (MaxHeadersTotalBytes < MinMaxHeadersTotalBytes || MaxHeadersTotalBytes > MaxMaxHeadersTotalBytes)
            {
                problems.Add(
                    $"{RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath} must be between {MinMaxHeadersTotalBytes} and {MaxMaxHeadersTotalBytes}.");
            }

            return problems;
        }
    }
}
