using System.Collections.Generic;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity
{
    public class UserEmailAddressesDto
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public List<UserEmailAddressDto> EmailAddresses { get; set; } = new();
    }
}
