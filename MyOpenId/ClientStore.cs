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
                string query = @"SELECT TOP 1 * FROM Clients WITH(NOLOCK) WHERE ClientId = @ClientId";

                DynamicParameters p = new();
                p.Add("ClientId", clientId);

                var result = await conn.QuerySingleAsync<OIDCClient>(query, p);
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
                string query = 
@" MERGE INTO Clients
AS target USING 
    (SELECT @ClientId AS ClientId, @ClientSecret AS ClientSecret, @AllowedScopes AS AllowedScopes) AS source 
ON target.ClientId = source.ClientId 
WHEN MATCHED THEN 
    UPDATE SET ClientSecret = source.ClientSecret, AllowedScopes = source.AllowedScopes 
WHEN NOT MATCHED THEN 
    INSERT (ClientId, ClientSecret, AllowedScopes) 
    VALUES (source.ClientId, source.ClientSecret, source.AllowedScopes);";

                DynamicParameters p = new();
                p.Add("ClientId", client.ClientId);
                p.Add("ClientSecret", hashed);
                p.Add("AllowedScopes", client.AllowedScopes);

                int result = await conn.ExecuteAsync(query, p);
                if (result == 0)
                {
                    throw new Exception("Failed to merge OIDC client");
                }

            }
        }
    }
}
