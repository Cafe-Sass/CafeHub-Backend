using System;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Persistence.Master.Identity
{
    public class PlatformAdminRoleClaim : IdentityRoleClaim<Guid>
    {
        public new Guid Id { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}

