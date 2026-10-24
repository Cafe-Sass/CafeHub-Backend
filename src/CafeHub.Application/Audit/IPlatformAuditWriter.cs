namespace CafeHub.Application.Audit
{
    public interface IPlatformAuditWriter
    {
        void Write(PlatformAuditEntry entry);
    }
}
