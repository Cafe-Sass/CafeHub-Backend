using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Onboarding.DTOs;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed record RejectOnboardingRequestCommand(Guid RequestId, string? Reason)
        : ICommand<RejectionResponse>, IIdempotentCommand, IMasterTransactionalCommand
    {
        public string? IdempotencyKey { get; set; }
    }
}
