namespace CafeHub.Domain.Tenants
{
    /// <summary>
    /// The operational status of a branch. Transitions are application-validated; a branch
    /// is never physically deleted (logical-deletion policy).
    /// </summary>
    public enum BranchLifecycleStatus
    {
        Active,
        Suspended,
        Retired,
    }
}
