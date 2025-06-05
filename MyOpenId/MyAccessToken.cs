using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class MyAccessToken
    {
        public required string TokenId { get; set; }
        public required string ClientId { get; set; }
        public required string Scopes { get; set; }
        public DateTime? Expires { get; set; }
        public DateTime? Created {  get; set; }
    }
}
