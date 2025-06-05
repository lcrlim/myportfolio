using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class OIDCClient
    {
        public required string ClientId { get; set; }
        public required string ClientSecret { get; set; }
        public required string AllowedScopes { get; set; }
    }
}
