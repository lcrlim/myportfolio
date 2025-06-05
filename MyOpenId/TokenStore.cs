using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class TokenStore : ITokenStore
    {
        private readonly IConnStringProvider connStringProvider;

        public TokenStore(IConnStringProvider connStringProvider)
        {
            this.connStringProvider = connStringProvider;
        }

        public async Task<MyAccessToken> FindAccessTokenAsync(string tokenId)
        {
            using (SqlConnection conn = new(connStringProvider.GetConnectionString()))
            {
                DynamicParameters p = new();
                p.Add("TokenId", tokenId);

                var result = await conn.QuerySingleAsync<MyAccessToken>("SpAccessTokenGet", p, commandType: CommandType.StoredProcedure);
                if (result == null)
                {
                    throw new Exception($"not exists token id - {tokenId}");
                }
                return result;
            }
        }

        public async Task RevokeAccessTokenAsync(string tokenId)
        {
            using (SqlConnection conn = new(connStringProvider.GetConnectionString()))
            {
                DynamicParameters p = new();
                p.Add("TokenId", tokenId);

                int result = await conn.ExecuteAsync("SpAccessTokenRemove", p, commandType: CommandType.StoredProcedure);
                if (result == 0)
                {
                    throw new Exception($"faild to remove");
                }
            }
        }

        public async Task SaveAccessTokenAsync(MyAccessToken token, long expiresIn)
        {
            using (SqlConnection conn = new(connStringProvider.GetConnectionString()))
            {
                DynamicParameters p = new();
                p.Add("TokenId", token.TokenId);
                p.Add("ClientId", token.ClientId);
                p.Add("Scopes", token.Scopes);
                p.Add("ExpiresIn", expiresIn);

                int result = await conn.ExecuteAsync("SpAccessTokenSet", p, commandType: CommandType.StoredProcedure);
                if (result == 0)
                {
                    throw new Exception($"faild to set");
                }
            }
        }
    }
}
