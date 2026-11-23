using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Orders.Queries
{
    public sealed class GetGuestOrderStatusQueryHandler(ISender sender, IGuestOrderStore guestOrderStore)
        : IQueryHandler<GetGuestOrderStatusQuery, GuestOrderStatus>
    {
        public async Task<Result<GuestOrderStatus>> Handle(
            GetGuestOrderStatusQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var reference = query.PublicReference?.Trim();
            if (!Order.IsValidPublicReference(reference))
            {
                return Result.Failure<GuestOrderStatus>(OrdersErrors.NotFound());
            }

            var entry = await guestOrderStore.FindByPublicReferenceAsync(reference!, cancellationToken);
            if (entry is null)
            {
                return Result.Failure<GuestOrderStatus>(OrdersErrors.NotFound());
            }

            if (entry.Status == nameof(OrderStatus.Pending))
            {
                await sender.Send(new ExpireStaleOrdersCommand(null, entry.Id), cancellationToken);
                entry = await guestOrderStore.FindByPublicReferenceAsync(reference!, cancellationToken);
                if (entry is null)
                {
                    return Result.Failure<GuestOrderStatus>(OrdersErrors.NotFound());
                }
            }

            return Result.Success(GuestOrderStatus.From(entry));
        }
    }
}
