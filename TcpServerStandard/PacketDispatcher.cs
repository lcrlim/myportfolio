using MyCommonNet;
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
        public async Task<MyPacket?> Dispatch(MyPacket req)
        {
            // 여기에서 데이터를 원하는 형태로 파싱하고 처리합니다.
            try
            {
                MyPacket res = new MyPacket();
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
            catch (Exception e)
            {
                Log.Warning($"Dispatch error - {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ping 패킷 처리
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public Task<PacketPong?> Ping(MyPacket packet)
        {
            PacketPing? req = null;
            if (packet?.Body != null)
            {
                req = JsonConvert.DeserializeObject<PacketPing>(packet.Body);
                if (req != null)
                {
                    Log.Logger.Information($"Packet arrived - Ping, Num:{req.Num}, Str:{req.Str}");

                    Thread.Sleep(2000);
                    return Task.FromResult<PacketPong?>(new PacketPong
                    {
                        Num = req.Num,
                        Str = req.Str,
                    });
                }
            }

            return Task.FromResult<PacketPong?>(null);
        }
    }
}
