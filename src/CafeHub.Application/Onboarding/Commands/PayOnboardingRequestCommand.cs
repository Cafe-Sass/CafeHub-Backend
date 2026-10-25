using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Onboarding.PaymentGateway;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed record PayOnboardingRequestCommand(Guid RequestId)
        : ICommand<PaymentOutcomeResponse>, IIdempotentCommand, IMasterTransactionalCommand
    {
        public string? IdempotencyKey { get; set; }
    }
}
