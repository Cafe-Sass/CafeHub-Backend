using System.Globalization;
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

        public static TenantAuditChanges StaffCreate(TenantUser user, Branch branch)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(TenantUser.FullName), null, user.FullName),
                new TenantAuditChanges.FieldChange(nameof(TenantUser.BranchId), null, branch.Id.ToString()),
                new TenantAuditChanges.FieldChange("IsActive", null, bool.TrueString),
            });
        }

        public static TenantAuditChanges StaffUpdate(
            TenantUser user,
            string fullName,
            Guid newBranchId)
        {
            ArgumentNullException.ThrowIfNull(user);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(TenantUser.FullName), user.FullName, fullName),
                new TenantAuditChanges.FieldChange(nameof(TenantUser.BranchId), user.BranchId?.ToString(), newBranchId.ToString()),
            });
        }

        public static TenantAuditChanges StaffLifecycleStatus(TenantUser user, bool newIsActive)
        {
            ArgumentNullException.ThrowIfNull(user);
            var currentIsActive = user.DeletedAt is null;
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange("IsActive", currentIsActive.ToString(), newIsActive.ToString()),
            });
        }

        public static TenantAuditChanges StationCreate(Station station)
        {
            ArgumentNullException.ThrowIfNull(station);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Station.Name), null, station.Name),
                new TenantAuditChanges.FieldChange(
                    nameof(Station.SortOrder),
                    null,
                    station.SortOrder.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(Station.Status), null, station.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Station.BranchId), null, station.BranchId.ToString()),
            });
        }

        public static TenantAuditChanges StationUpdate(Station station, string name, int sortOrder)
        {
            ArgumentNullException.ThrowIfNull(station);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Station.Name), station.Name, name),
                new TenantAuditChanges.FieldChange(
                    nameof(Station.SortOrder),
                    station.SortOrder.ToString(CultureInfo.InvariantCulture),
                    sortOrder.ToString(CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges StationLifecycleStatus(Station station, StationStatus newStatus)
        {
            ArgumentNullException.ThrowIfNull(station);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(Station.Status),
                    station.Status.ToString(),
                    newStatus.ToString()),
            });
        }

        public static TenantAuditChanges TableCreate(BranchTable table)
        {
            ArgumentNullException.ThrowIfNull(table);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Number), null, table.Number.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Label), null, table.Label),
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Status), null, table.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(BranchTable.BranchId), null, table.BranchId.ToString()),
            });
        }

        public static TenantAuditChanges TableUpdate(BranchTable table, string? label)
        {
            ArgumentNullException.ThrowIfNull(table);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Label), table.Label, label),
            });
        }

        public static TenantAuditChanges TableBulkCreate(int count, int fromNumber, int toNumber)
        {
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange("count", null, count.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("fromNumber", null, fromNumber.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("toNumber", null, toNumber.ToString(CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges TableBulkDeactivate(int count, int fromNumber, int toNumber)
        {
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange("count", null, count.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("fromNumber", null, fromNumber.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("toNumber", null, toNumber.ToString(CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges TableLifecycleStatus(BranchTable table, BranchTableStatus newStatus)
        {
            ArgumentNullException.ThrowIfNull(table);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(BranchTable.Status),
                    table.Status.ToString(),
                    newStatus.ToString()),
            });
        }
    }
}
