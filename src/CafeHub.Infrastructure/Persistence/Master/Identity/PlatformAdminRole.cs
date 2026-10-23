using System;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Persistence.Master.Identity
{
    public class PlatformAdminRole : IdentityRole<Guid>
    {
        public PlatformAdminRole()
        {
            Id = Guid.Empty;
            ConcurrencyStamp = Guid.NewGuid().ToString();
        }

        public PlatformAdminRole(string roleName) : this()
        {
            Name = roleName;
        }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }
    }
}

