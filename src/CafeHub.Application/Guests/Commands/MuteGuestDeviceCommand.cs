using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Guests.DTOs;

namespace CafeHub.Application.Guests.Commands
{
    public sealed record MuteGuestDeviceCommand : ICommand<GuestMuteResult>, ITenantTransactionalCommand, ISensitiveRequest
    {
        public Guid OrderId { get; init; }

        public string Reason { get; init; } = string.Empty;

        public int? ExpiryHours { get; init; }
    }
}
