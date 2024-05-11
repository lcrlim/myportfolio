using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonNetwork
{
    /// <summary>
    /// 샘플 ping 패킷
    /// </summary>
    public class PacketPing
    {
        public int Num { get; set; }
        public string? Str { get; set; }
    }

    /// <summary>
    /// 샘플 pong 패킷
    /// </summary>
    public class PacketPong
    {
        public int Num { get; set; }
        public string? Str { get; set; }
    }
}
