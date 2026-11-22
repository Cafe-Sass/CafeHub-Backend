using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Orders.DTOs;

namespace CafeHub.Application.Orders.Commands
{
    public sealed record AcceptOrderCommand(Guid OrderId) : ICommand<OrderDecisionResult>;
}
