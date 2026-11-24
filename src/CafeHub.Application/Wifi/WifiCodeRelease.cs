namespace CafeHub.Application.Wifi
{
    public static class WifiCodeRelease
    {
        public static bool IsReleased(
            decimal orderTotal,
            decimal orderPaid,
            decimal? billTotal,
            decimal? billPaid)
        {
            if (orderPaid >= orderTotal)
            {
                return true;
            }

            return billTotal is { } total
                && billPaid is { } paid
                && paid >= total;
        }
    }
}
