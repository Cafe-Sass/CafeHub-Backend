using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed record DeclineRenewalRequestCommand(
        Guid RenewalRequestId,
        Guid TenantKey,
        string? ActorReference) : ICommand<SubscriptionRenewalResponse>;
}
