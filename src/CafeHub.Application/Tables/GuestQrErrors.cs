using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tables
{
    public static class GuestQrErrors
    {
        public static Error NotFound() => Error.NotFound("GuestQr.NotFound", "The table QR was not found.");
    }
}
