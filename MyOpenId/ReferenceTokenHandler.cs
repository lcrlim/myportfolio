using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class ReferenceTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly ITokenStore tokenStore;

        public ReferenceTokenHandler(
            ITokenStore tokenStore,
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) 
            : base (options, logger, encoder)
        {
            this.tokenStore = tokenStore;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.NoResult();

            var authHeader = Request.Headers["Authorization"].ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            var endpoint = Context.GetEndpoint();
            if (endpoint != null)
            {
                var authorizeAttr = endpoint.Metadata.GetMetadata<AuthorizeAttribute>();
                if (authorizeAttr == null)
                    return AuthenticateResult.NoResult();
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var accessToken = await tokenStore.FindAccessTokenAsync(token);

            if (accessToken == null || accessToken.Expires <= DateTime.UtcNow)
            {
                return AuthenticateResult.Fail("invalid or expired token");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, accessToken.ClientId)
            };

            if (!string.IsNullOrEmpty(accessToken.Scopes))
            {
                var scopes = accessToken.Scopes.Split(',');
                foreach (var scope in scopes)
                {
                    claims.Add(new Claim("scope", scope));
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var tiket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(tiket);
        }
    }
}
