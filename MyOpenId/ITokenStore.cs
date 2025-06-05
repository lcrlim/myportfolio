using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public interface ITokenStore
    {
        Task SaveAccessTokenAsync(MyAccessToken token, long expiresIn);
        Task<MyAccessToken> FindAccessTokenAsync(string tokenId);
        Task RevokeAccessTokenAsync(string tokenId);
    }
}
