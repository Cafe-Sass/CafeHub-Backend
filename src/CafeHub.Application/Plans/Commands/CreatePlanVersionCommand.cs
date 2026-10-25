using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Plans.DTOs;

namespace CafeHub.Application.Plans.Commands
{
    public sealed record CreatePlanVersionCommand(
        Guid PlanId,
        decimal Price,
        string Currency,
        string Period,
        IReadOnlyList<PlanEntitlementRequest> Entitlements)
        : ICommand<PlanResponse>, IMasterTransactionalCommand;
}
