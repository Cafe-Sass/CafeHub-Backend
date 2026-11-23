using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Media
{
    public static class MediaErrors
    {
        public const string InvalidFileCode = "Media.InvalidFile";
        public const string TooLargeCode = "Media.TooLarge";
        public const string KeyExistsCode = "Media.KeyExists";
        public const string StorageFailedCode = "Media.StorageFailed";

        public static Error InvalidFile() =>
            Error.Validation(InvalidFileCode, "The file is empty or its content is not a supported type.");

        public static Error TooLarge() =>
            Error.Validation(TooLargeCode, "The file exceeds the configured size limit.");

        public static Error KeyExists() =>
            Error.Conflict(KeyExistsCode, "A file already exists for this media key.");

        public static Error StorageFailed() =>
            Error.Failure(StorageFailedCode, "The file could not be stored.");
    }
}
