using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed record ApproveRenewalRequestCommand(
        Guid RenewalRequestId,
        string? ActorReference) : ICommand<SubscriptionRenewalResponse>;
}
