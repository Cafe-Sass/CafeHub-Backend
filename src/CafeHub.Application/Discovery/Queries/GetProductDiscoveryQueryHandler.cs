using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tables.Queries;
using MediatR;

namespace CafeHub.Application.Discovery.Queries
{
    public sealed class GetProductDiscoveryQueryHandler(ISender sender, ProductDiscoveryGenerator generator)
        : IQueryHandler<GetProductDiscoveryQuery, ProductDiscoveryResult>
    {
        public async Task<Result<ProductDiscoveryResult>> Handle(
            GetProductDiscoveryQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var context = await sender.Send(new ResolveGuestQrQuery(query.Code), cancellationToken);
            if (context.IsFailure)
            {
                return Result.Failure<ProductDiscoveryResult>(context.Error!);
            }

            var discovery = await generator.DiscoverAsync(
                context.Value.BranchId,
                ProductDiscoveryText.Normalize(query.Query),
                cancellationToken);
            return Result.Success(discovery);
        }
    }
}
