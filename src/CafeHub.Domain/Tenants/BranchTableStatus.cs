namespace CafeHub.Domain.Tenants
{
    /// <summary>
    /// The operational status of a branch table. Deactivation is a status change; a table
    /// is never physically deleted (logical-deletion policy).
    /// </summary>
    public enum BranchTableStatus
    {
        Active,
        Inactive,
    }
}
