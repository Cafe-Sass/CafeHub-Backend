namespace CafeHub.Application.Identity
{
    public static class TenantPermissions
    {
        public const string ClaimType = "cafehub.permission";
        public const string StaffRead = "staff.read";
        public const string StationsManage = "stations.manage";
        public const string TablesManage = "tables.manage";
        public const string MenuAvailability = "menu.availability";
        public const string MenuManage = "menu.manage";
        public const string OrdersView = "orders.view";
        public const string OrdersDecide = "orders.decide";
        public const string OrdersCaptain = "orders.captain";
        public const string MissesView = "orders.misses.view";
        public const string StaffManage = "staff.manage";
        public const string RolesManage = "roles.manage";
        public const string CafeProfileManage = "cafe-profile.manage";
        public const string GuestsMute = "guests.mute";
        public const string GuestsMuteAudit = "guests.mute.audit";
        public const string BranchesRead = "branches.read";
        public const string BranchesManage = "branches.manage";

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
            new[] { BranchesManage, BranchesRead, CafeProfileManage, GuestsMute, GuestsMuteAudit, MenuAvailability, MenuManage, OrdersCaptain, OrdersDecide, MissesView, OrdersView, RolesManage, StaffManage, StaffRead, StationsManage, TablesManage });

        public static bool IsOwnerOnly(string? code) => code is RolesManage or BranchesManage or CafeProfileManage;

        public static bool IsStaffGrant(string? code) =>
            !IsOwnerOnly(code) && All.Contains(code, StringComparer.Ordinal);

        public static IReadOnlyList<string> StaffUnion(IEnumerable<string?> grants) =>
            All.Where(code => !IsOwnerOnly(code) && grants.Contains(code, StringComparer.Ordinal)).ToArray();
    }
}
