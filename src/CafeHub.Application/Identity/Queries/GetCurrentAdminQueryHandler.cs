using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity.DTOs;

namespace CafeHub.Application.Identity.Queries
{
    public sealed class GetCurrentAdminQueryHandler : IQueryHandler<GetCurrentAdminQuery, CurrentAdminResponse>
    {
        private readonly ICurrentUser currentUser;
        private readonly IAdminAccountReader adminAccountReader;

        public GetCurrentAdminQueryHandler(ICurrentUser currentUser, IAdminAccountReader adminAccountReader)
        {
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            this.adminAccountReader = adminAccountReader ?? throw new ArgumentNullException(nameof(adminAccountReader));
        }

        public async Task<Result<CurrentAdminResponse>> Handle(GetCurrentAdminQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var adminId = currentUser.AdminId;
            if (!currentUser.IsAuthenticated || adminId is null || adminId == Guid.Empty)
            {
                return Result.Failure<CurrentAdminResponse>(AuthErrors.CurrentAdminUnavailable());
            }

            var administrator = await adminAccountReader.FindActiveByIdAsync(adminId.Value, cancellationToken);
            if (administrator is null)
            {
                return Result.Failure<CurrentAdminResponse>(AuthErrors.CurrentAdminUnavailable());
            }

            return Result.Success(new CurrentAdminResponse
            {
                Id = administrator.Id,
                Email = administrator.Email,
                Roles = administrator.Roles,
            });
        }
    }
}
