using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record SetBranchProductAvailabilityCommand(Guid BranchId, Guid ProductId, bool Available, string Reason)
        : ICommand<BranchProductAvailabilityResponse>;
}
