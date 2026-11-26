using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed record CreateRenewalRequestCommand(
        Guid SubscriptionId,
        Guid? PlanVersionId,
        string? ActorReference) : ICommand<SubscriptionRenewalResponse>;
}
