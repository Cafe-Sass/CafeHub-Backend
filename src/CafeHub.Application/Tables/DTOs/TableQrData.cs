namespace CafeHub.Application.Tables.DTOs
{
    public sealed class TableQrData(Guid tableId, int number, string? label, string code, uint version)
    {
        public Guid TableId { get; } = tableId;
        public int Number { get; } = number;
        public string? Label { get; } = label;
        public string Code { get; } = code;
        public uint Version { get; } = version;
    }
}
