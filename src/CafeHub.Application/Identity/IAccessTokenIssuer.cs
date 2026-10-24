namespace CafeHub.Application.Identity
{
    public interface IAccessTokenIssuer
    {
        AccessToken Issue(AccessTokenRequest request);
    }
}
