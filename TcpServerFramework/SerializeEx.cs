using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerFramework
{
    public static class SerializeEx
    {
        public static Newtonsoft.Json.JsonSerializerSettings st = new Newtonsoft.Json.JsonSerializerSettings
        {
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate,
        };

        public static byte[] Serialize(Packet obj)
        {
            byte[] type = BitConverter.GetBytes(obj.Type);
            byte[] body = Encoding.UTF8.GetBytes(obj.Body);
            byte[] len = BitConverter.GetBytes(body.Length);
            byte[] combiled = new byte[len.Length + type.Length + body.Length];
            len.CopyTo(combiled, 0);
            type.CopyTo(combiled, len.Length);
            body.CopyTo(combiled, len.Length + type.Length);
            return combiled;
        }
    }
}
