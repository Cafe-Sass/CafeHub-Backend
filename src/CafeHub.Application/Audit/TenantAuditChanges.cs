namespace CafeHub.Application.Audit
{
    public sealed class TenantAuditChanges
    {
        private readonly IReadOnlyList<FieldChange> fields;

        private TenantAuditChanges(bool isCreate, IEnumerable<FieldChange> fields)
        {
            ArgumentNullException.ThrowIfNull(fields);
            IsCreate = isCreate;
            this.fields = Array.AsReadOnly(fields.ToArray());
        }

        public bool IsCreate { get; }

        public IReadOnlyList<FieldChange> Fields => fields;

        public static TenantAuditChanges Create(IEnumerable<FieldChange> fields) => new(true, fields);

        public static TenantAuditChanges Update(IEnumerable<FieldChange> fields) => new(false, fields);

        public sealed record FieldChange(string Name, string? Before, string? After);
    }
}
