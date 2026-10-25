using CafeHub.Domain.Plans;

namespace CafeHub.UnitTests.Plans.Domain
{
    internal static class PlanTestData
    {
        public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public static readonly DateTimeOffset CreatedAt = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        public static Plan CreatePlan(
            string code = "starter",
            string displayName = "Starter",
            bool isSelectable = true,
            Guid? id = null)
        {
            return Plan.Create(id ?? Guid.NewGuid(), code, displayName, isSelectable, CreatedAt, AdminId);
        }

        public static PlanEntitlement CreateEntitlement(
            Guid planVersionId,
            string key = "max_tables",
            string value = "10",
            Guid? id = null)
        {
            return PlanEntitlement.Create(id ?? Guid.NewGuid(), planVersionId, key, value, CreatedAt);
        }

        public static PlanVersion AddVersion(
            Plan plan,
            Guid? versionId = null,
            decimal price = 19.99m,
            string currencyCode = "usd",
            BillingPeriod billingPeriod = BillingPeriod.Monthly,
            PlanVersionStatus status = PlanVersionStatus.Active,
            IEnumerable<PlanEntitlement>? entitlements = null)
        {
            return plan.AddVersion(
                versionId ?? Guid.NewGuid(),
                price,
                currencyCode,
                billingPeriod,
                status,
                entitlements ?? Array.Empty<PlanEntitlement>(),
                CreatedAt,
                AdminId);
        }
    }
}
