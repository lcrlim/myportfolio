using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using System.Runtime.CompilerServices;

namespace CommonNetwork
{
    /// <summary>
    /// 서버 객체
    /// </summary>
    public class TcpServer
    {
        private TcpListener? server;
        private IPacketDispatcher dispatcher;

        /// <summary>
        /// 서버 시작
        /// </summary>
        /// <param name="port"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public async Task Start(int port, IPacketDispatcher dispatcher, CancellationToken ctoken)
        {
            if (server == null)
            {
                server = new TcpListener(IPAddress.Any, port);
            }
            if (dispatcher == null)
            {
                Log.Logger.Error("Packet dispatcher is null");
                throw new Exception("Packet dispatcher is null");
            }

            this.dispatcher = dispatcher;

            server.Start();
            ctoken.Register(server.Stop);

            // 여기서 부터 비동기로 실행되도록 yield하여 thread pool 에서 accept 작업을 실행하도록 한다.
            await Task.Yield();
            Log.Information($"TCP Server started - Port:{port}, IsThreadPool:{Thread.CurrentThread.IsThreadPoolThread}");

            while (!ctoken.IsCancellationRequested)
            {
                try
                {
                    // accept connection
                    TcpClient conn = await server.AcceptTcpClientAsync(ctoken).ConfigureAwait(false);
                    Log.Information($"New connection arrived - {conn.Client.RemoteEndPoint}");

                    // 신규 연결 시 새로운 워커 생성 후 Run
                    var work = new ClientWorker(conn, this.dispatcher, ctoken);
                    //_ = Task.Run(work.RunReadAsync, ctoken);
                    _ = work.RunReadAsync();
                }
                catch (OperationCanceledException)
                {
                    Log.Logger.Warning("Tcp server terminate signal");
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"Error during accept - {ex.ToString()}");
                }
            }
        }

        /// <summary>
        /// 서버 종료 시 처리할 것들 구현
        /// </summary>
        public void Stop() 
        {
            server?.Dispose();
        }

    }
}
