using CommonNetwork;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerStandard
{
    public class PacketDispatcher : IPacketDispatcher
    {
        /// <summary>
        /// 데이터를 파싱하고 처리하는 메서드
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public async Task<PacketBase?> Dispatch(PacketBase req)
        {
            // 여기에서 데이터를 원하는 형태로 파싱하고 처리합니다.
            PacketBase res = new PacketBase();
            switch (req.Type)
            {
                case (int)Packet.Type.PING:
                    {
                        var pong = await Ping(req).ConfigureAwait(false);
                        res.Type = (int)Packet.Type.PONG;
                        res.Body = JsonConvert.SerializeObject(pong);
                        break;
                    }
                default:
                    {
                        Log.Logger.Information($"Undefined packet type - PacketType:{req.Type}");
                        return null;
                    }
            }
            return res;
        }

        /// <summary>
        /// Ping 패킷 처리
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public async Task<PacketPong> Ping(PacketBase packet)
        {
            PacketPing? req = JsonConvert.DeserializeObject<PacketPing>(packet.Body);

            Log.Logger.Information($"Packet arrived - Ping, Num:{req.Num}, Str:{req.Str}");
            Thread.Sleep(2000);
            return new PacketPong
            {
                Num = req.Num,
                Str = req?.Str,
            };
        }
    }
}
