using Skoruba.IdentityServer4.Admin.Configuration.Constants;
using Skoruba.IdentityServer4.Admin.Configuration.Interfaces;

namespace Skoruba.IdentityServer4.Admin.Configuration
{
    public class AdminConfiguration : IAdminConfiguration
    {
        public string IdentityAdminBaseUrl { get; set; }

        public string IdentityAdminRedirectUri { get; set; }

        public string IdentityServerBaseUrl { get; set; }
        public string ClientId { get; set; } = AuthenticationConsts.OidcClientId;
        public string[] Scopes { get; set; }

        public string IdentityAdminApiSwaggerUIClientId { get; } 
        public string IdentityAdminApiSwaggerUIRedirectUrl { get; } 
        public string IdentityAdminApiScope { get; } 

        public string ClientSecret { get; set; } = AuthenticationConsts.OidcClientSecret;
        public string OidcResponseType { get; set; } = AuthenticationConsts.OidcResponseType;


    }
}
