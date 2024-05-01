using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;

namespace TcpServerStandard
{
    public class ProcessPacket
    {
        public static char[]? Process(int type, PacketBase packet)
        {
            char[]? result = null;
            switch (type)
            {
                case (int)Packet.Type.PING:
                    {
                        PacketPing req = (PacketPing)packet;
                        var res = new PacketPong
                        {
                            Num = req.Num, Str = req.Str
                        };
                        result = JsonConvert.SerializeObject(res).ToCharArray();
                        break;
                    }
                default:
                    {
                        Log.Logger.Information($"Undefined packet type - {type}");
                        break;
                    }
            }
            
            return result;
        }
    }
}
