using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tables
{
    public static class GuestQrErrors
    {
        public const string NotFoundCode = "GuestQr.NotFound";

        public static Error NotFound() => Error.NotFound(NotFoundCode, "The table QR was not found.");
    }
}
