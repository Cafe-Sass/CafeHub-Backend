namespace CafeHub.Domain.Tenants
{
    public sealed record PrinterProfileDefinition(
        PrinterProfile Profile,
        int LineWidth,
        string CodePage);

    public static class PrinterProfiles
    {
        private static readonly IReadOnlyDictionary<PrinterProfile, PrinterProfileDefinition> Definitions =
            new Dictionary<PrinterProfile, PrinterProfileDefinition>
            {
                [PrinterProfile.Escpos80mm] = new(PrinterProfile.Escpos80mm, 48, "CP437"),
                [PrinterProfile.Escpos58mm] = new(PrinterProfile.Escpos58mm, 32, "CP437"),
            };

        public static IReadOnlyList<PrinterProfileDefinition> All { get; } =
            Definitions.Values.OrderBy(definition => definition.Profile).ToArray();

        public static bool TryGet(PrinterProfile profile, out PrinterProfileDefinition? definition) =>
            Definitions.TryGetValue(profile, out definition);
    }
}
