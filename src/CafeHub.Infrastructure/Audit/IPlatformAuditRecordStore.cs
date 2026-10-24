using CafeHub.Infrastructure.Persistence.Master.Audit;

namespace CafeHub.Infrastructure.Audit
{
    internal interface IPlatformAuditRecordStore
    {
        void Add(PlatformAuditRecord record);
    }
}
