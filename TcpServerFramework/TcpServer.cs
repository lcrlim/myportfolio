using System;
using System.Text;
using System.Threading.Tasks;
using Nito.Async.Sockets;
using Serilog;
using Newtonsoft.Json;

namespace TcpServerFramework
{
    /// <summary>
    /// 기본 패킷 클래스, Type으로 패킷 종류를 구분하고, Body는 Json string을 기본으로한다.
    /// 별도의 직렬화 라이브러리를 사용하면 그에 맞게 변경하면 된다.
    /// </summary>
    public class Packet
    {
        public int Type { get; set; }   // packet type으로 별도 enum으로 정의해서 사용하면 된다.
        public string Body { get; set; }    // json serialized string
    }

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
            // process request

            // make response
            var res = new Packet();

            // serialize response and send to client
            socket.WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(res)));
        }
    }
}
