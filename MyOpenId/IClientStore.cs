using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public interface IClientStore
    {
        Task<OIDCClient> FindClientByIdAsync(string clientId);
        Task SaveClientAsync(OIDCClient client);
    }
}
