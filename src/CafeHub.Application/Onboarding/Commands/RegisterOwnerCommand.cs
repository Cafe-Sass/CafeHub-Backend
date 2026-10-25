using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Onboarding;

namespace CafeHub.Application.Onboarding.Commands
{
    public sealed record RegisterOwnerCommand(
        string OwnerEmail,
        string OwnerFullName,
        string? OwnerPhone,
        string CafeName,
        string Password,
        string Slug,
        Guid PlanVersionId)
        : ICommand<OwnerRegistrationResponse>, IMasterTransactionalCommand;
}
