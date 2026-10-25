using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Plans.DTOs;

namespace CafeHub.Application.Plans.Queries
{
    public sealed record GetPlanQuery(Guid PlanId) : IQuery<PlanResponse>;
}
