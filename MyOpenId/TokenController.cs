using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    [Route("connect")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IClientStore clientStore;
        private readonly ITokenStore tokenStore;

        public TokenController(IClientStore clientStore, ITokenStore tokenStore)
        {
            this.clientStore = clientStore;
            this.tokenStore = tokenStore;
        }

        [HttpPost]
        [Route("token")]
        public async Task<IActionResult> CreateToken([FromForm] TokenRequest body)
        {
            if (body.grant_type.Equals("client_credentials", StringComparison.OrdinalIgnoreCase) == false)
                return BadRequest("unsupported grant_type");

            if (!Request.Headers.ContainsKey("Authorization")
                || !Request.Headers["Authorization"].ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return Unauthorized();

            var authHeader = Request.Headers["Authorization"].ToString().Substring("Basic ".Length).Trim();
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader)).Split(':');
            if (credentials.Length != 2)
                return Unauthorized();

            string? clientId = credentials[0];
            string? clientSecret = credentials[1];

            // secret validation
            var client = await clientStore.FindClientByIdAsync(clientId);
            if (client == null 
                || client.ClientSecret != HashHelper.ComputeSha256(clientSecret))
                return Unauthorized();

            // scope validation
            var requestedScopes = body.scope?.Split(',') ?? Array.Empty<string>();
            var allowedScopes = client.AllowedScopes?.Split(',') ?? Array.Empty<string>();
            if (!requestedScopes.All(s => allowedScopes.Contains(s)))
                return Unauthorized();

            var tokenType = body.token_type ?? "reference";
            long expiresIn = 3600;
            var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            var scopes = string.Join(",", requestedScopes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, client.ClientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };

            foreach (var scope in requestedScopes)
            {
                claims.Add(new Claim("scope", scope));
            }

            if (tokenType.Equals("reference", StringComparison.OrdinalIgnoreCase))
            {
                string tokenId = Guid.NewGuid().ToString("N");
                await tokenStore.SaveAccessTokenAsync(new MyAccessToken
                {
                    TokenId = tokenId,
                    ClientId = client.ClientId,
                    Scopes = scopes,
                }, expiresIn);

                Console.WriteLine($"Token created - Type:reference, ClientId:{client.ClientId}, Scopes:{scopes}, ExpiresIn:{expiresIn}");

                return Ok(new
                { 
                    access_token = tokenId,
                    token_type = "Bearer",
                    expires_in = expiresIn,
                });
            }
            else if (tokenType.Equals("jwt", StringComparison.OrdinalIgnoreCase))
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MyOpenIdStatics.JwtKey));  // 암호화 관리 필요
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: MyOpenIdStatics.Issuer,
                    audience: MyOpenIdStatics.Audience,
                    claims: claims,
                    expires: expiresAt,
                    signingCredentials: creds);

                Console.WriteLine($"Token created - Type:jwt, ClientId:{client.ClientId}, Scopes:{scopes}, ExpiresAt:{expiresAt.ToString("u")}");

                return Ok(new
                {
                    access_token = new JwtSecurityTokenHandler().WriteToken(token),
                    token_type = "Bearer",
                    expires_in = expiresIn
                });
            }
            else
            {
                return BadRequest("unsupported token_type");
            }
        }

        [HttpPost]
        [Route("revoke")]
        public async Task<IActionResult> Revoke([FromBody] string token)
        {
            await tokenStore.RevokeAccessTokenAsync(token);

            Console.WriteLine($"Token removed - Token:{token}");

            return Ok();
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] OIDCClient client)
        {
            await clientStore.SaveClientAsync(client);

            Console.WriteLine($"Client created - ClientId:{client.ClientId}");

            return Ok();
        }
    }
}
