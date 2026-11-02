using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;

namespace CafeHub.Application.CafeProfile.Queries
{
    public sealed class GetCafeProfileQueryHandler : IQueryHandler<GetCafeProfileQuery, CafeProfileResponse>
    {
        private readonly ICafeSettingsReader reader;

        public GetCafeProfileQueryHandler(ICafeSettingsReader reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public async Task<Result<CafeProfileResponse>> Handle(GetCafeProfileQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var response = await reader.GetAsync(cancellationToken);
            if (response is null)
            {
                return Result.Failure<CafeProfileResponse>(CafeProfileErrors.NotFound());
            }

            return Result.Success(response);
        }
    }
}
