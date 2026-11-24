using System.Reflection;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantErdChecklistTests
    {
        private static readonly string[] ErdTables =
        [
            "tenant_users",
            "tenant_roles",
            "tenant_user_roles",
            "tenant_user_claims",
            "tenant_role_claims",
            "branches",
            "branch_tables",
            "stations",
            "menu_categories",
            "products",
            "product_sizes",
            "product_addons",
            "product_tags",
            "branch_product_availability",
            "product_branch_station_slots",
            "product_station_assignments",
            "product_routing_readiness",
            "tenant_user_stations",
            "cafe_settings",
            "cafe_payment_settings",
            "tenant_audit_records",
            "refresh_tokens",
            "orders",
            "order_items",
            "order_item_addons",
            "order_number_counters",
            "order_response_misses",
            "order_response_miss_holders",
            "guest_mutes",
            "bills",
            "payments",
            "receipts",
            "receipt_lines",
            "receipt_payments",
            "receipt_number_counters",
            "wifi_settings",
            "branch_wifi_networks",
        ];

        [Fact]
        public void EveryErdTable_HasExactlyOneDomainEntity()
        {
            var entityTypes = typeof(TenantUser).Assembly
                .GetTypes()
                .Where(t => t.Namespace == "CafeHub.Domain.Tenants" && t.IsClass && !t.IsAbstract && !t.IsEnum)
                .Where(t => !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                .Where(t => t.Name is not nameof(Tenant) and not nameof(TenantDatabase) and not nameof(TenantSchemaUpgradeRun))
                .ToList();

            var entityNames = entityTypes
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToList();

            var expectedEntityNames = ErdTables
                .Select(table => table switch
                {
                    "tenant_users" => nameof(TenantUser),
                    "tenant_roles" => nameof(TenantRole),
                    "tenant_user_roles" => nameof(TenantUserRole),
                    "tenant_user_claims" => nameof(TenantUserClaim),
                    "tenant_role_claims" => nameof(TenantRoleClaim),
                    "branches" => nameof(Branch),
                    "branch_tables" => nameof(BranchTable),
                    "stations" => nameof(Station),
                    "menu_categories" => nameof(MenuCategory),
                    "products" => nameof(Product),
                    "product_sizes" => nameof(ProductSize),
                    "product_addons" => nameof(ProductAddon),
                    "product_tags" => nameof(ProductTag),
                    "branch_product_availability" => nameof(BranchProductAvailability),
                    "product_branch_station_slots" => nameof(ProductBranchStationSlot),
                    "product_station_assignments" => nameof(ProductStationAssignment),
                    "product_routing_readiness" => nameof(ProductRoutingReadiness),
                    "tenant_user_stations" => nameof(TenantUserStation),
                    "cafe_settings" => nameof(CafeSettings),
                    "cafe_payment_settings" => nameof(CafePaymentSettings),
                    "tenant_audit_records" => nameof(TenantAuditRecord),
                    "refresh_tokens" => nameof(TenantRefreshToken),
                    "orders" => nameof(Order),
                    "order_items" => nameof(OrderItem),
                    "order_item_addons" => nameof(OrderItemAddon),
                    "order_number_counters" => nameof(OrderNumberCounter),
                    "order_response_misses" => nameof(OrderResponseMiss),
                    "order_response_miss_holders" => nameof(OrderResponseMissHolder),
                    "guest_mutes" => nameof(GuestMute),
                    "bills" => nameof(Bill),
                    "payments" => nameof(Payment),
                    "receipts" => nameof(Receipt),
                    "receipt_lines" => nameof(ReceiptLine),
                    "receipt_payments" => nameof(ReceiptPayment),
                    "receipt_number_counters" => nameof(ReceiptNumberCounter),
                    "wifi_settings" => nameof(WifiSettings),
                    "branch_wifi_networks" => nameof(BranchWifiNetwork),
                    _ => throw new InvalidOperationException($"Unknown ERD table: {table}"),
                })
                .OrderBy(n => n)
                .ToList();

            Assert.Equal(expectedEntityNames, entityNames);
        }

        [Fact]
        public void NoDomainType_ReferencesEfCoreOrAspNetCoreIdentity()
        {
            var tenantDomainAssembly = typeof(TenantUser).Assembly;

            var referencedAssemblyNames = tenantDomainAssembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencedAssemblyNames);
            Assert.DoesNotContain("Microsoft.AspNetCore.Identity", referencedAssemblyNames);
        }
    }
}
