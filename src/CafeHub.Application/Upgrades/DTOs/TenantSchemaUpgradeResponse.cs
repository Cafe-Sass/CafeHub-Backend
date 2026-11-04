namespace CafeHub.Application.Upgrades.DTOs
{
    public sealed record TenantSchemaUpgradeResponse(
        Guid RunId,
        Guid TenantId,
        string Status,
        string TargetMigrationId,
        Guid? RequestedBy,
        DateTimeOffset CreatedAt,
        Guid? CreatedBy);
}
