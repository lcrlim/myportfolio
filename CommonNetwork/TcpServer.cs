using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

namespace MyCommonNet
{
    /// <summary>
    /// 서버 객체
    /// </summary>
    public class TcpServer
    {
        private TcpListener? server;
        private IPacketDispatcher? dispatcher;
        private ObjectPool<ClientWorker>? clientPool;
        private long connectionIdCounter = 0;

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

            InitClientPool(10000);

            this.dispatcher = dispatcher;

            server.Start(1000); // 백로그 1000 설정
            ctoken.Register(server.Stop);

            // 여기서 부터 비동기로 실행되도록 yield하여 thread pool 에서 accept 작업을 실행하도록 한다.
            await Task.Yield();

            // 단일 루프 대신, 여러 개의 Accept 루프를 동시에 돌려 동시 접속 요청을 빠르게 처리한다.
            int acceptThreadCount = Environment.ProcessorCount; // 코어 수 만큼만
            Log.Information("TCP Server started - Port:{Port}, Backlog:5000, AcceptThreads:{AcceptThreadCount}", port, acceptThreadCount);

            // 병렬 accept를 위한 반복 호출
            var acceptTasks = new Task[acceptThreadCount];
            for (int i = 0; i < acceptThreadCount; i++)
            {
                acceptTasks[i] = RunAcceptLoopAsync(ctoken);
            }

            // 모든 Accept 루프가 종료되어 서버가 종료될 때까지 대기
            await Task.WhenAll(acceptTasks);
        }

        /// <summary>
        /// 클라이언트 풀을 초기에 생성해 둡니다.
        /// </summary>
        /// <param name="initialSize"></param>
        private void InitClientPool(int initialSize)
        {
            // 1. 풀 생성 (최대 풀 사이즈 등 설정 가능)
            var provider = new DefaultObjectPoolProvider();

            // 풀에 보관할 최대 객체 수. 
            provider.MaximumRetained = initialSize;

            clientPool = provider.Create(new ClientWorkerPolicy());

            Log.Information("Warming up client pool...");
            var preAllocated = new List<ClientWorker>(initialSize);
            for (int i = 0; i < initialSize; i++)
            {
                preAllocated.Add(clientPool.Get());
            }
            foreach (var worker in preAllocated)
            {
                clientPool.Return(worker);
            }
            Log.Information("Client pool warmed up - {InitialSize}", initialSize);
        }

        /// <summary>
        /// Accept 루프
        /// </summary>
        private async Task RunAcceptLoopAsync(CancellationToken ctoken)
        {
            while (!ctoken.IsCancellationRequested)
            {
                TcpClient? conn = null;
                try
                {
                    // 멀티 스레드 환경에서도 TcpListener.AcceptTcpClientAsync는 스레드 안전하게 백로그 큐를 공유합니다.
                    conn = await server.AcceptTcpClientAsync(ctoken).ConfigureAwait(false);

                    long newId = Interlocked.Increment(ref connectionIdCounter);

                    Log.Information("New connection({RemoteEndPoint}, Id:{NewId}) arrived", conn.Client.RemoteEndPoint, newId);

                    // Socket 설정 최적화
                    conn.NoDelay = true;
                    conn.ReceiveBufferSize = 8192;
                    conn.SendBufferSize = 8192;
                    conn.LingerState = new LingerOption(true, 0);   // timedwait 없이 바로 삭제 처리

                    // 풀링된 객체 사용
                    var work = clientPool.Get();
                    work.SetClient(newId, conn, this.dispatcher, ctoken);

                    // 읽기 작업 시작
                    _ = work.RunReadAsync(clientPool);
                }
                catch (OperationCanceledException)
                {
                    // 서버 종료 시그널
                    Log.Logger.Fatal("Serve stop signal arrived");

                    if (conn != null)
                    {
                        conn.Dispose();
                        conn = null;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    // 예외 발생 시 로그만 찍고 루프는 유지해야 함
                    Log.Logger.Error("Error during accept loop - {Message}", ex.Message);

                    if (conn != null)
                    {
                        conn.Dispose();
                        conn = null;
                    }
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
