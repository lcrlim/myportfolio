using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerFramework
{
    /// <summary>
    /// 기본 패킷 클래스, Type으로 패킷 종류를 구분하고, Body는 Json string을 기본으로한다.
    /// 별도의 직렬화 라이브러리를 사용하면 그에 맞게 변경하면 된다.
    /// </summary>
    public class Packet
    {
        public int Type { get; set; }   // packet type으로 별도 enum으로 정의해서 사용하면 된다.
        public string Body { get; set; }    // json serialized string
    }

    public enum PacketType
    {
        PING = 0,
        PONG = 1,
    }

    public class PacketPing
    {
        public string Str { get; set; }
    }

    public class PacketPong
    {
        public string Str { get; set; }
    }
}
