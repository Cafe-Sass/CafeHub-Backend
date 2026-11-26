using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed record TransitionSubscriptionCommand(
        Guid SubscriptionId,
        SubscriptionStatus TargetStatus,
        string? Reason) : ICommand<SubscriptionLifecycleResponse>;
}
