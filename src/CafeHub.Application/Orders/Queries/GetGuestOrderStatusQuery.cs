using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Orders.DTOs;

namespace CafeHub.Application.Orders.Queries
{
    public sealed record GetGuestOrderStatusQuery(string? PublicReference) : IQuery<GuestOrderStatus>;
}
