using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public static class PlatformSeedErrors
    {
        public const string InvalidConfigurationCode = "PlatformSeed.InvalidConfiguration";
        public const string InvalidConfigurationMessage = "Platform seed configuration is invalid";

        public static Error InvalidConfiguration()
        {
            return Error.Failure(InvalidConfigurationCode, InvalidConfigurationMessage);
        }
    }
}
