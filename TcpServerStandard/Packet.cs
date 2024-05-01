using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerStandard
{
    /// <summary>
    /// 패킷 기본 클래스
    /// </summary>
    public class PacketBase
    {

    }

    /// <summary>
    /// 샘플 ping 패킷
    /// </summary>
    public class PacketPing : PacketBase
    {
        public int Num { get; set; }
        public string? Str { get; set; }
    }

    /// <summary>
    /// 샘플 pong 패킷
    /// </summary>
    public class PacketPong : PacketBase
    {
        public int Num { get; set; }
        public string? Str { get; set; }
    }
}
