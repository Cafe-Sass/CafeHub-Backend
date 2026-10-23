using System;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Persistence.Master.Identity
{
    public class PlatformAdminUser : IdentityUser<Guid>
    {
        public PlatformAdminUser()
        {
            Id = Guid.Empty;
            LockoutEnabled = true;
        }

        public PlatformAdminUser(string userName) : this()
        {
            UserName = userName;
        }

        public DateTimeOffset? DeletedAt { get; set; }

        public Guid? DeletedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public bool IsActive
        {
            get => DeletedAt is null;
            set => DeletedAt = value ? null : (DeletedAt ?? DateTimeOffset.UtcNow);
        }
    }
}

