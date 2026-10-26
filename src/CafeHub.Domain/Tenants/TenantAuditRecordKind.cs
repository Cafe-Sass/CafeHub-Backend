namespace CafeHub.Domain.Tenants
{
    /// <summary>
    /// The kind of actor that wrote a tenant audit record: an authenticated person or the
    /// system (for example the provisioning seed). Owner versus staff is not stored
    /// redundantly; it is derived through the actor user and <see cref="TenantUserKind"/>.
    /// </summary>
    public enum TenantAuditRecordKind
    {
        User,
        System,
    }
}
