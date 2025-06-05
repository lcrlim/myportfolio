using System.Security.Cryptography;
using System.Text;

namespace MyOpenId
{
    public static class HashHelper
    {
        public static string ComputeSha256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
