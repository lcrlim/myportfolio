using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class TokenRequest
    {
        public required string grant_type { get; set; }
        public required string scope { get; set; }
        public required string token_type { get; set; } = "reference"; // jwt or reference
    }

    public class RegisterRequest
    {
        public required string ClientId { get; set; }

        public required string ClientSecret { get; set; }
        public required string AllowedScopes { get; set; }  // comma string "aaa,bbb"
    }
}
