using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MyCommonNet.Packet;

namespace MyCommonNet
{
    [PacketType((int)PacketType.PING)]
    public sealed class PacketPing : MyPacket
    {
        public int Num { get; set; }
        public string Str { get; set; } = string.Empty;
    }

    [PacketType((int)PacketType.PONG)]
    public sealed class PacketPong : MyPacket
    {
        public int Num { get; set; }
        public string Str { get; set; } = string.Empty;
    }

    [PacketType((int)PacketType.LOGIN)]
    public sealed class PacketLogin : MyPacket
    {
        public string UserId { get; set; } = string.Empty;
    }

    [PacketType((int)PacketType.LOGIN_RESULT)]
    public sealed class PacketLoginResult : MyPacket
    {
        public bool Success { get; set; }
    }
}
