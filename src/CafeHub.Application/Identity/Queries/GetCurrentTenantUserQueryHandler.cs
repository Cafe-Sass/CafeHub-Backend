using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Identity.Queries
{
    public sealed class GetCurrentTenantUserQueryHandler : IQueryHandler<GetCurrentTenantUserQuery, TenantMeResponse>
    {
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantUserAuthenticator tenantUserAuthenticator;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public GetCurrentTenantUserQueryHandler(
            ICurrentTenantUser currentTenantUser,
            ITenantUserAuthenticator tenantUserAuthenticator,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantUserAuthenticator = tenantUserAuthenticator ?? throw new ArgumentNullException(nameof(tenantUserAuthenticator));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<TenantMeResponse>> Handle(GetCurrentTenantUserQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var tenantContext = tenantContextAccessor.GetRequired();
            var userId = currentTenantUser.UserId;
            if (!currentTenantUser.IsAuthenticated
                || userId is null
                || userId == Guid.Empty
                || currentTenantUser.TenantId != tenantContext.TenantId
                || !tenantContext.IsActive
                || string.IsNullOrWhiteSpace(tenantContext.Slug))
            {
                return Unavailable();
            }

            var user = await tenantUserAuthenticator.FindByIdAsync(userId.Value, cancellationToken);
            if (user is null)
            {
                return Unavailable();
            }

            return Result.Success(new TenantMeResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                UserKind = user.UserKind,
                Roles = user.Roles,
                TenantSlug = tenantContext.Slug,
            });
        }

        private static Result<TenantMeResponse> Unavailable()
        {
            return Result.Failure<TenantMeResponse>(AuthErrors.CurrentTenantUserUnavailable());
        }
    }
}
