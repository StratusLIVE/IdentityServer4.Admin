using Skoruba.AuditLogging.Events;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Events.Identity
{
    public class UserEmailAddressesRequestedEvent : AuditEvent
    {
        public UserEmailAddressesDto EmailAddresses { get; set; }

        public UserEmailAddressesRequestedEvent(UserEmailAddressesDto emailAddresses)
        {
            EmailAddresses = emailAddresses;
        }
    }
}
