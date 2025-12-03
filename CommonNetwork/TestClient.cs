using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json; // System.Text.Json 사용

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
        private async Task<T?> SendAndReceive<T>(MyPacket req)
        {
            await parser.WritePacket(req);
            MyPacket res = await parser.ReadPacket();

            // Body가 있으면 Body 사용 (레거시), 없으면 BodyMemory 사용
            if (!string.IsNullOrEmpty(res.Body))
            {
                return JsonSerializer.Deserialize<T>(res.Body);
            }
            
            if (!res.BodyMemory.IsEmpty)
            {
                 return JsonSerializer.Deserialize<T>(res.BodyMemory.Span);
            }

            return default(T);
        }

        public async Task<PacketPong?> Ping(int pingNumber, string pingString)
        {
            byte[] bodyBytes = JsonSerializer.SerializeToUtf8Bytes(new PacketPing
            {
                Num = pingNumber,
                Str = pingString
            });

            var req = new MyPacket
            {
                Type = (int)Packet.PacketType.PING,
                BodyMemory = bodyBytes,
                Len = Packet.PACKET_HEADER_SIZE + bodyBytes.Length
            };

            return await SendAndReceive<PacketPong>(req);
        }

        public async Task<PacketLoginResult?> Login(string userId)
        {
            byte[] bodyBytes = JsonSerializer.SerializeToUtf8Bytes(new PacketLogin
            {
                UserId = userId
            });

            var req = new MyPacket
            {
                Type = (int)Packet.PacketType.LOGIN,
                BodyMemory = bodyBytes,
                Len = Packet.PACKET_HEADER_SIZE + bodyBytes.Length
            };

            return await SendAndReceive<PacketLoginResult>(req);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
