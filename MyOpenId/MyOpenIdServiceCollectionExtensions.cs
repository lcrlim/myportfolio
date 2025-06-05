using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MyOpenId
{
    public static class MyOpenIdServiceCollectionExtensions
    {
        public static IServiceCollection AddMyOpenId(this IServiceCollection services, Func<string> getConnectionString)
        {
            services.AddSingleton<IConnStringProvider, ConnStringProvider>(c => new ConnStringProvider(getConnectionString));
            services.AddSingleton<IClientStore, ClientStore>();
            services.AddSingleton<ITokenStore, TokenStore>();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "hybrid";
                    options.DefaultChallengeScheme = "hybrid";
                })
                .AddJwtBearer(options =>
                {
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MyOpenIdStatics.JwtKey));
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = MyOpenIdStatics.Issuer,
                        ValidAudience = MyOpenIdStatics.Audience,
                        IssuerSigningKey = key,
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, ReferenceTokenHandler>("ReferenceToken", null)
                .AddPolicyScheme("hybrid", "hybrid", options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var authHeader = context.Request.Headers["Authorization"].ToString();
                        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authHeader.Contains("."))
                            return JwtBearerDefaults.AuthenticationScheme;
                        return "ReferenceToken";
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(MyOpenIdScopes.Auth, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", MyOpenIdScopes.Auth);
                });
                options.AddPolicy(MyOpenIdScopes.Bill, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", MyOpenIdScopes.Bill);
                });
                options.AddPolicy(MyOpenIdScopes.Admin, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", MyOpenIdScopes.Admin);
                });
            });

            return services;
        }
    }
}
