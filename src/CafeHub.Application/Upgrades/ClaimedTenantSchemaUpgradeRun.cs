namespace CafeHub.Application.Upgrades
{
    public sealed record ClaimedTenantSchemaUpgradeRun(Guid RunId, Guid TenantId);
}
