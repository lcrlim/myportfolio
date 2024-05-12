using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 테스트용 클라이언트
    /// </summary>
    public class TestClient : IDisposable
    {
        /// <summary>
        /// tcp clinet
        /// </summary>
        private TcpClient client = new TcpClient();

        /// <summary>
        /// 패킷 룰 파서
        /// </summary>
        private PacketParser parser = new PacketParser();

        /// <summary>
        ///  연결
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task ConnectAsync(string host, int port)
        {
            await client.ConnectAsync(host, port);
            parser.SetStream(client.GetStream());
        }

        /// <summary>
        /// 요청 보내고 응답 수신
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="req"></param>
        /// <returns></returns>
        private async Task<T> SendAndReceive<T>(MyPacket req)
        {
            await parser.WritePacket(req);
            MyPacket res = await parser.ReadPacket();

            return JsonConvert.DeserializeObject<T>(res.Body);
        }

        public async Task<PacketPong> Ping(int pingNumber, string pingString)
        {
            var req = new MyPacket
            {
                Type = (int)Packet.Type.PING,
                Body = JsonConvert.SerializeObject(new PacketPing
                {
                    Num = pingNumber,
                    Str = pingString
                })
            };
            req.Len = Packet.PACKET_HEADER_SIZE + req.Body.Length;

            return await SendAndReceive<PacketPong>(req);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
