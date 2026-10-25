namespace CafeHub.Infrastructure.Onboarding.Payments
{
    public sealed class PaymentOptions
    {
        public string Provider { get; set; } = "Mock";
        public MockPaymentOptions Mock { get; set; } = new();
    }

    public sealed class MockPaymentOptions
    {
        public string Outcome { get; set; } = "Succeed";
        public bool AllowOutsideDevelopment { get; set; } = false;
    }
}
