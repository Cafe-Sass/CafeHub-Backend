using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Plans.DTOs;

namespace CafeHub.Application.Plans.Commands
{
    public sealed record CreatePlanCommand(string Key, string Name, bool IsSelectable)
        : ICommand<PlanResponse>, IMasterTransactionalCommand;
}
