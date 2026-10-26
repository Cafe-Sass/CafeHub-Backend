namespace CafeHub.Application.Identity
{
    public interface ITenantAccessTokenIssuer
    {
        AccessToken Issue(TenantAccessTokenRequest request);
    }
}
