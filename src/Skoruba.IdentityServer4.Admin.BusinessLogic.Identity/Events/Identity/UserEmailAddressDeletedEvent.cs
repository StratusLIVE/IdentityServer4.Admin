using Skoruba.AuditLogging.Events;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Events.Identity
{
    public class UserEmailAddressDeletedEvent : AuditEvent
    {
        public UserEmailAddressDto EmailAddress { get; set; }

        public UserEmailAddressDeletedEvent(UserEmailAddressDto emailAddress)
        {
            EmailAddress = emailAddress;
        }
    }
}
