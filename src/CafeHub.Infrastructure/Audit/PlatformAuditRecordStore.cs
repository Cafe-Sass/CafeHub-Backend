using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Audit;

namespace CafeHub.Infrastructure.Audit
{
    internal sealed class PlatformAuditRecordStore(MasterDbContext context) : IPlatformAuditRecordStore
    {
        public void Add(PlatformAuditRecord record)
        {
            context.PlatformAuditRecords.Add(record);
        }
    }
}
