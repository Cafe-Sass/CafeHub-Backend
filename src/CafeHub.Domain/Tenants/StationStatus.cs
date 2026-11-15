namespace CafeHub.Domain.Tenants
{
    /// <summary>
    /// The operational status of a preparation station. Deactivation is a status change; a station
    /// is never physically deleted (logical-deletion policy).
    /// </summary>
    public enum StationStatus
    {
        Active,
        Inactive,
    }
}
