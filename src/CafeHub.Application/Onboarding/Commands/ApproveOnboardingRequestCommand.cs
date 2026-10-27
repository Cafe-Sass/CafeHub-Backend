using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Onboarding.DTOs;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed record ApproveOnboardingRequestCommand(Guid RequestId)
        : ICommand<ApprovalResponse>, IIdempotentCommand
    {
        public string? IdempotencyKey { get; set; }
    }
}
