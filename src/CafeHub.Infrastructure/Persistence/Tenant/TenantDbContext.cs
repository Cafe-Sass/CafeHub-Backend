using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Tenant
{
    public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : DbContext(options)
    {
        public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
        public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
        public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();
        public DbSet<TenantUserClaim> TenantUserClaims => Set<TenantUserClaim>();
        public DbSet<TenantRoleClaim> TenantRoleClaims => Set<TenantRoleClaim>();
        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Station> Stations => Set<Station>();
        public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
        public DbSet<ProductBranchStationSlot> ProductBranchStationSlots => Set<ProductBranchStationSlot>();
        public DbSet<ProductStationAssignment> ProductStationAssignments => Set<ProductStationAssignment>();
        public DbSet<ProductRoutingReadiness> ProductRoutingReadiness => Set<ProductRoutingReadiness>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductSize> ProductSizes => Set<ProductSize>();
        public DbSet<ProductAddon> ProductAddons => Set<ProductAddon>();
        public DbSet<ProductTag> ProductTags => Set<ProductTag>();
        public DbSet<BranchProductAvailability> BranchProductAvailability => Set<BranchProductAvailability>();
        public DbSet<BranchTable> BranchTables => Set<BranchTable>();
        public DbSet<TenantUserStation> TenantUserStations => Set<TenantUserStation>();
        public DbSet<CafeSettings> CafeSettings => Set<CafeSettings>();
        public DbSet<CafePaymentSettings> CafePaymentSettings => Set<CafePaymentSettings>();
        public DbSet<TenantAuditRecord> TenantAuditRecords => Set<TenantAuditRecord>();
        public DbSet<TenantRefreshToken> TenantRefreshTokens => Set<TenantRefreshToken>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<OrderItemAddon> OrderItemAddons => Set<OrderItemAddon>();
        public DbSet<OrderNumberCounter> OrderNumberCounters => Set<OrderNumberCounter>();
        public DbSet<OrderResponseMiss> OrderResponseMisses => Set<OrderResponseMiss>();
        public DbSet<OrderResponseMissHolder> OrderResponseMissHolders => Set<OrderResponseMissHolder>();
        public DbSet<GuestMute> GuestMutes => Set<GuestMute>();
        public DbSet<Bill> Bills => Set<Bill>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Receipt> Receipts => Set<Receipt>();
        public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
        public DbSet<ReceiptPayment> ReceiptPayments => Set<ReceiptPayment>();
        public DbSet<ReceiptNumberCounter> ReceiptNumberCounters => Set<ReceiptNumberCounter>();
        public DbSet<WifiSettings> WifiSettings => Set<WifiSettings>();
        public DbSet<BranchWifiNetwork> BranchWifiNetworks => Set<BranchWifiNetwork>();
        public DbSet<WifiPackage> WifiPackages => Set<WifiPackage>();
        public DbSet<BranchWifiQr> BranchWifiQr => Set<BranchWifiQr>();
        public DbSet<WifiVoucher> WifiVouchers => Set<WifiVoucher>();
        public DbSet<Printer> Printers => Set<Printer>();
        public DbSet<PrintRoute> PrintRoutes => Set<PrintRoute>();
        public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

        public DbSet<PrintConnector> PrintConnectors => Set<PrintConnector>();

        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(TenantDbContext).Assembly,
                type => type.Namespace == "CafeHub.Infrastructure.Persistence.Tenant.Configurations");
        }
    }
}
