using System;
using System.Text;
using System.Threading.Tasks;
using Nito.Async.Sockets;
using Serilog;
using Newtonsoft.Json;

namespace TcpServerFramework
{
    /// <summary>
    /// TCP 서버 객체
    /// 기본적으로 single thread에서 socket accept를 처리하고, 이후 패킷 처리는 Task를 생성해 ThreadPool에서 처리되도록 한다.
    /// 적당히 높은 성능을 처리할 수 있을 것으로 예상되나, 극도로 높은 부하가 발생할 때 이를 처리하기에 적합하진 않다.
    /// </summary>
    public class TcpServer
    {
        MyServerSocket serverSocket;    // Nito라이브러리의 server socket의 wrapper

        public void Start(int port)
        {
            // 서버 소켓 생성 및 초기화
            if (serverSocket != null)
            {
                serverSocket.Stop();
            }
            serverSocket = new MyServerSocket(port);

            // 패킷 수신 처리 코드 구현
            serverSocket.RecvAction = (c, input) =>
            {
                Packet req = null;
                try
                {
                    req = JsonConvert.DeserializeObject<Packet>(input.ToString());

                    Log.Logger.Information($"Packet arrived - Type:{req.Type}, Len:{req.Body.Length}, From:{c.RemoteEndPoint.ToString()}");
                    _ = Task.Run(() => ProcessPacket(req, c));                    
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"Process packet error - {ex.ToString()}");
                }
            };

            Log.Logger.Information($"TcpServer started - Port:{port}");
        }

        /// <summary>
        /// 서버 객체 close
        /// </summary>
        public void Stop()
        {
            serverSocket.Stop();
        }

        /// <summary>
        /// 패킷 처리 로직을 구현해서 사용하면 된다.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="socket"></param>
        public void ProcessPacket(Packet req, SimpleServerChildTcpSocket socket)
        {
            Packet res = null;

            // process request
            switch (req.Type)
            {
                case (int)PacketType.PING:
                    {
                        var pong = new PacketPong { };
                        res = new Packet
                        {
                            Type = (int)PacketType.PONG,
                            Body = JsonConvert.SerializeObject(pong)
                        };
                        break;
                    }
                default:
                    {
                        Log.Logger.Warning($"Undefiled packet type - {req.Type}");
                        break;
                    }
            }

            // make response
            

            // serialize response and send to client
            if (res != null)
            {
                socket.WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(res)));
            }
        }
    }
}
