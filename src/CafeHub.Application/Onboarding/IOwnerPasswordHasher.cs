namespace CafeHub.Application.Onboarding
{
    /// <summary>
    /// Hashes the owner's registration password through the Identity password hasher. The raw password never
    /// leaves the API layer and the hash is never returned by any endpoint.
    /// </summary>
    public interface IOwnerPasswordHasher
    {
        string Hash(string password);
    }
}
