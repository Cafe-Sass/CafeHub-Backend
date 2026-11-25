namespace CafeHub.Application.Printing
{
    public sealed record PrintPayloadLine(string Text, string Alignment, bool Emphasized);

    public sealed record PrintDocumentPayload(
        string DocumentType,
        string SourceId,
        IReadOnlyList<PrintPayloadLine> Lines);

    public static class PrintPayloadAlignments
    {
        public const string Left = "left";
        public const string Center = "center";
        public const string Right = "right";
    }
}
