using System;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Persistence.Master.Identity
{
    public class PlatformAdminUserClaim : IdentityUserClaim<Guid>
    {
        public new Guid Id { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}

