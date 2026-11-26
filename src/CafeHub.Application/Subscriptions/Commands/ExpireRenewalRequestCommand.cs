using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Subscriptions.DTOs;

namespace CafeHub.Application.Subscriptions.Commands
{
    public sealed record ExpireRenewalRequestCommand(Guid RenewalRequestId)
        : ICommand<SubscriptionRenewalResponse>;
}
