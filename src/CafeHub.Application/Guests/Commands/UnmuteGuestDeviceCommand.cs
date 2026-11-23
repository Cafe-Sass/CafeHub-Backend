using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Guests.DTOs;

namespace CafeHub.Application.Guests.Commands
{
    public sealed record UnmuteGuestDeviceCommand : ICommand<GuestMuteResult>, ITenantTransactionalCommand
    {
        public Guid MuteId { get; init; }
    }
}
