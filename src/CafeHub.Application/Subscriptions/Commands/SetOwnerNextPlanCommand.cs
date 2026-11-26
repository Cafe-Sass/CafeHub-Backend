using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed record SetOwnerNextPlanCommand(
        Guid TenantKey,
        Guid PlanVersionId,
        string? ActorReference) : ICommand<SubscriptionRenewalResponse>;
}
