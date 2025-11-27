using MyCommonNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MyCommonNet.Packet;

namespace TcpServerStandard
{
    /// <summary>
    /// Login 패킷 핸들러
    /// </summary>
    public sealed class PacketLoginHandler : IPacketHandler<PacketLogin>
    {
        public async Task<MyPacket?> HandleAsync(PacketLogin packet, CancellationToken cancellationToken = default)
        {
            // 응답 패킷 생성 (MyPacket 상속 타입)
            var pong = new PacketLoginResult
            {
                Type = (int)PacketType.LOGIN_RESULT,
                Success = true
            };

            return pong;
        }
    }
}
