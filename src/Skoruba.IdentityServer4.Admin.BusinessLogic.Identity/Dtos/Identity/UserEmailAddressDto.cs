using System.ComponentModel.DataAnnotations;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity
{
    public class UserEmailAddressDto
    {
        public string EmailAddressId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public bool EmailConfirmed { get; set; }
        public bool IsPrimary { get; set; }
    }
}
