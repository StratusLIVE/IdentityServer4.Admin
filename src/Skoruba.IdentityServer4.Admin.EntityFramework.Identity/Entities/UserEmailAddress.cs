namespace Skoruba.IdentityServer4.Admin.EntityFramework.Identity.Entities
{
    // Maps to the platform's custom UserEmailAddresses table (owned by IdentityServer's
    // PlatformDbContext; this app never migrates it).
    public class UserEmailAddress
    {
        // Mirrors IdentityServer's AccountEmailService cap. Shared so the service-layer check and
        // the primary-row sync in the repository can't drift apart.
        public const int MaxPerUser = 3;

        public string Id { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
        public string NormalizedEmail { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool IsPrimary { get; set; }
    }
}
