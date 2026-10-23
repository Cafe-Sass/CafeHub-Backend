using System;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Persistence.Master.Identity
{
    public class PlatformAdminUserRole : IdentityUserRole<Guid>
    {
        public Guid Id { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}

