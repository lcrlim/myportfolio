using MyCommonNet;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MyCommonNet.Packet;

namespace TcpServerStandard
{
    /// <summary>
    /// Ping 패킷 핸들러 (async-only)
    /// </summary>
    public sealed class PacketPingHandler : IPacketHandler<PacketPing>
    {
        public PacketPingHandler()
        {
        }

        public async Task<MyPacket?> HandleAsync(PacketPing packet, CancellationToken cancellationToken = default)
        {
            //Log.Information($"Ping packet arrived - Num:{packet.Num}, Str:{packet.Str}");
            // 응답 패킷 생성 (MyPacket 상속 타입)
            var pong = new PacketPong
            {
                Type = (int)PacketType.PONG,
                Num = packet.Num,   // ping pong 동일 값
                Str = packet.Str,   // ping pong 동일 값
            };

            return pong;
        }
    }
}
