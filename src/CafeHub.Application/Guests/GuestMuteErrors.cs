using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Guests
{
    public static class GuestMuteErrors
    {
        public const string NotFoundCode = "Guests.NotFound";
        public const string DeviceRequiredCode = "Guests.DeviceRequired";
        public const string ReasonInvalidCode = "Guests.ReasonInvalid";
        public const string ExpiryInvalidCode = "Guests.ExpiryInvalid";
        public const string BranchRequiredCode = "Guests.BranchRequired";
        public const string DateRangeInvalidCode = "Guests.DateRangeInvalid";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The order or guest mute was not found.");

        public static Error DeviceRequired() =>
            Error.Validation(DeviceRequiredCode, "The order has no guest device to mute.");

        public static Error ReasonInvalid() =>
            Error.Validation(ReasonInvalidCode, "The mute reason must be 3 to 256 characters with no control characters.");

        public static Error ExpiryInvalid() =>
            Error.Validation(ExpiryInvalidCode, "The mute expiry must be between 1 and 168 hours.");

        public static Error BranchRequired() =>
            Error.Validation(BranchRequiredCode, "The branch id is required.");

        public static Error DateRangeInvalid() =>
            Error.Validation(DateRangeInvalidCode, "The summary date range must end on or after its start.");
    }
}
