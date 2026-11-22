namespace CafeHub.Application.Orders
{
    public static class OrdersConfigurationKeys
    {
        public const string SectionName = "Orders";
        public const string ResponseWindowSecondsKey = "ResponseWindowSeconds";

        public const string ResponseWindowSecondsPath = $"{SectionName}:{ResponseWindowSecondsKey}";

        public const int DefaultResponseWindowSeconds = 300;
    }
}
