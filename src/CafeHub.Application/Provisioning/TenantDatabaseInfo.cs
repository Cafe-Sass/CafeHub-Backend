namespace CafeHub.Application.Provisioning
{
    /// <summary>
    /// The ensured tenant database reference: names only, plus the credential reference label.
    /// Never carries a password or connection string.
    /// </summary>
    public sealed record TenantDatabaseInfo(
        string DatabaseName,
        string OwnerRoleName,
        string ApplicationRoleName,
        string CredentialReference);
}
