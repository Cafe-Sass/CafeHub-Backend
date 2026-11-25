namespace CafeHub.Domain.Tenants
{
    public static class PrintFailureCodes
    {
        public const string PrinterOffline = nameof(PrinterOffline);
        public const string PaperOut = nameof(PaperOut);
        public const string PayloadRejected = nameof(PayloadRejected);
        public const string Unknown = nameof(Unknown);

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
            new[] { PrinterOffline, PaperOut, PayloadRejected, Unknown });

        public static bool IsSupported(string? code) =>
            code is not null && All.Contains(code, StringComparer.Ordinal);
    }
}
