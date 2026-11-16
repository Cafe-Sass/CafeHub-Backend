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
            "tenant_user_stations",
            "cafe_settings",
            "tenant_audit_records",
            "refresh_tokens",
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
                    "tenant_user_stations" => nameof(TenantUserStation),
                    "cafe_settings" => nameof(CafeSettings),
                    "tenant_audit_records" => nameof(TenantAuditRecord),
                    "refresh_tokens" => nameof(TenantRefreshToken),
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
