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
    public class ClientStore : IClientStore
    {
        private readonly IConnStringProvider connStringProvider;

        public ClientStore(IConnStringProvider connStringProvider)
        {
            this.connStringProvider = connStringProvider;
        }

        public async Task<OIDCClient> FindClientByIdAsync(string clientId)
        {
            using (SqlConnection conn = new(connStringProvider.GetConnectionString()))
            {
                DynamicParameters p = new();
                p.Add("ClientId", clientId);

                var result = await conn.QuerySingleAsync<OIDCClient>("SpOIDCClientGet", p, commandType: CommandType.StoredProcedure);
                if (result == null)
                {
                    throw new Exception($"not exists client id - {clientId}");
                }
                return result;
            }
        }

        public async Task SaveClientAsync(OIDCClient client)
        {
            string hashed = HashHelper.ComputeSha256(client.ClientSecret);

            using (SqlConnection conn = new(connStringProvider.GetConnectionString()))
            {
                DynamicParameters p = new();
                p.Add("ClientId", client.ClientId);
                p.Add("ClientSecret", hashed);
                p.Add("AllowedScopes", client.AllowedScopes);

                int result = await conn.ExecuteAsync("SpOIDCClientSet", p, commandType: CommandType.StoredProcedure);                
                if (result == 0)
                {
                    throw new Exception($"failed to set");
                }
            }
        }
    }
}
