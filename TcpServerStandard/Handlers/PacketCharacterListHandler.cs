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
    /// Login 패킷 핸들러
    /// </summary>
    public sealed class PacketCharacterListHandler : IPacketHandler<PacketCharacterList>
    {
        public async Task<MyPacket?> HandleAsync(PacketCharacterList packet, CancellationToken cancellationToken = default)
        {
            //Log.Information($"Login packet arrived - UserId:{packet.UserId}");
            // 응답 패킷 생성 (MyPacket 상속 타입)
            var res = new PacketCharacterListResult
            {
                Type = (int)PacketType.CHARACTER_LIST_RESULT,
                Characters = new List<Character>
                {
                    new Character { Id = 1, Name = "Hero", Class = 1, Level = 10 },
                    new Character { Id = 2, Name = "Mage", Class = 2, Level = 8 },
                    new Character { Id = 3, Name = "Rogue", Class = 3, Level = 12 },
                }
            };

            return res;
        }
    }
}
