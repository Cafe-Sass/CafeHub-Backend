using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record SetProductStationAssignmentCommand(
        Guid ProductId, Guid BranchId, Guid? StationId, string ConcurrencyToken)
        : ICommand<ProductStationAssignmentResponse>, ITenantTransactionalCommand;
}
