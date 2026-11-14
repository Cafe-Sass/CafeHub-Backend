using CafeHub.Application.Audit;
using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Audit
{
    internal static class TenantAuditChangeSets
    {
        public static TenantAuditChanges CafeProfileUpdate(
            CafeSettings settings,
            string cafeName,
            string? primaryColor,
            string? secondaryColor,
            string? accentColor,
            string timezone)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.CafeName), settings.CafeName, cafeName),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.PrimaryColor), settings.PrimaryColor, primaryColor),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.SecondaryColor), settings.SecondaryColor, secondaryColor),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.AccentColor), settings.AccentColor, accentColor),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.Timezone), settings.Timezone, timezone),
            });
        }

        public static TenantAuditChanges BranchCreate(Branch branch)
        {
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Branch.Name), null, branch.Name),
                new TenantAuditChanges.FieldChange(nameof(Branch.OpeningHoursSummary), null, branch.OpeningHoursSummary),
                new TenantAuditChanges.FieldChange(nameof(Branch.LifecycleStatus), null, branch.LifecycleStatus.ToString()),
            });
        }

        public static TenantAuditChanges BranchUpdate(
            Branch branch,
            string name,
            string? openingHoursSummary)
        {
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Branch.Name), branch.Name, name),
                new TenantAuditChanges.FieldChange(nameof(Branch.OpeningHoursSummary), branch.OpeningHoursSummary, openingHoursSummary),
            });
        }

        public static TenantAuditChanges BranchLifecycleStatus(Branch branch, BranchLifecycleStatus newStatus)
        {
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(Branch.LifecycleStatus),
                    branch.LifecycleStatus.ToString(),
                    newStatus.ToString()),
            });
        }
    }
}
