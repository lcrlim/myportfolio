using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace TcpServerStandard
{
    /// <summary>
    /// 서버 객체
    /// </summary>
    public class TcpServer
    {
        private TcpListener? server;

        /// <summary>
        /// 서버 시작
        /// </summary>
        /// <param name="port"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public async Task Start(int port, CancellationToken ctoken)
        {
            if (server == null)
            {
                server = new TcpListener(IPAddress.Any, port);
            }

            server.Start();
            ctoken.Register(server.Stop);
            Log.Information($"TCP Server started - Port:{port}");            

            while (!ctoken.IsCancellationRequested)
            {
                try
                {
                    // accept connection
                    TcpClient conn = await server.AcceptTcpClientAsync(ctoken).ConfigureAwait(false);
                    Log.Information($"New connection arrived - {conn.Client.RemoteEndPoint}");

                    var work = new WorkTask(conn, ctoken);
                    _ = Task.Run(work.Run, ctoken);
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
