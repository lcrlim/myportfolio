using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// 서버 객체 - 설정에 따라 NetworkStream 또는 Pipeline 방식 사용
    /// Channel 기반 송신 큐와 SessionManager로 고성능 브로드캐스트 지원
    /// 내장 모니터링 기능으로 CPU, 메모리, 패킷 처리량 추적
    /// </summary>
    public class TcpServer
    {
        private TcpListener? server;
        private IPacketDispatcher? dispatcher;
        private IClientWorkerFactory? workerFactory;
        private object? clientPool;
        private long connectionIdCounter = 0;
        private TcpServerOptions options;
        private volatile bool isMonitoringEnabled = true;

        // static 변수 (ClientWorker 등 다른 클래스에서 참조용)
        private static volatile bool isMonitoringStatic = true;

        /// <summary>
        /// 모니터링 출력 활성화 상태
        /// </summary>
        public bool IsMonitoringEnabled => isMonitoringEnabled;

        /// <summary>
        /// 모니터링 상태 (static) - 다른 클래스에서 참조용
        /// </summary>
        public static bool IsMonitoring => isMonitoringStatic;

        /// <summary>
        /// 세션 관리자 (브로드캐스트 및 푸시 알림용)
        /// </summary>
        public SessionManager Sessions { get; } = new SessionManager();

        /// <summary>
        /// 기본 생성자 (NetworkStream 방식)
        /// </summary>
        public TcpServer() : this(new TcpServerOptions())
        {
        }

        /// <summary>
        /// 설정 옵션을 받는 생성자
        /// </summary>
        public TcpServer(TcpServerOptions options)
        {
            this.options = options ?? new TcpServerOptions();
            this.isMonitoringEnabled = this.options.EnableMonitoring;
            isMonitoringStatic = this.isMonitoringEnabled;
        }

        /// <summary>
        /// 모니터링 출력 활성화
        /// </summary>
        public void MonitorOn()
        {
            isMonitoringEnabled = true;
            isMonitoringStatic = true;
        }

        /// <summary>
        /// 모니터링 출력 비활성화
        /// </summary>
        public void MonitorOff()
        {
            isMonitoringEnabled = false;
            isMonitoringStatic = false;
        }

        /// <summary>
        /// 모니터링 출력 토글
        /// </summary>
        public void ToggleMonitor()
        {
            isMonitoringEnabled = !isMonitoringEnabled;
            isMonitoringStatic = isMonitoringEnabled;
            Console.WriteLine($"\n[System] Monitor Output: {(isMonitoringEnabled ? "ON" : "OFF")}");
        }

        /// <summary>
        /// 서버 시작
        /// </summary>
        /// <param name="port">포트 번호</param>
        /// <param name="dispatcher">패킷 디스패처</param>
        /// <param name="ctoken">취소 토큰</param>
        public async Task Start(int port, IPacketDispatcher dispatcher, CancellationToken ctoken)
        {
            if (server == null)
            {
                server = new TcpListener(IPAddress.Any, port);
            }
            if (dispatcher == null)
            {
                Log.Error("Packet dispatcher is null");
                throw new Exception("Packet dispatcher is null");
            }

            // 패킷 풀 초기화
            MyPacketPool.Initialize(options.PacketPoolSize);
            Log.Information("MyPacketPool initialized - {MaxRetained}", options.PacketPoolSize);

            // 팩토리 생성 (Parser 타입에 따라 결정)
            Func<IPacketParser> parserFactory;
            if (options.UsePipeline)
            {
                parserFactory = () => new PipelinePacketParser(options);
                Log.Information("Using Pipeline mode for packet processing");
            }
            else
            {
                parserFactory = () => new PacketParser(options);
                Log.Information("Using NetworkStream mode for packet processing");
            }

            // 통합 팩토리 및 풀 초기화
            workerFactory = new ClientWorkerFactory(parserFactory);
            clientPool = workerFactory.CreatePool(options.MaxPoolSize);

            this.dispatcher = dispatcher;

            server.Start(1000);
            ctoken.Register(server.Stop);

            int acceptThreadCount = Environment.ProcessorCount;
            string mode = options.UsePipeline ? "Pipeline" : "NetworkStream";
            Log.Information("TCP Server started - Port:{Port}, Mode:{Mode}, Backlog:1000, AcceptThreads:{AcceptThreadCount}",
                port, mode, acceptThreadCount);

            // 모니터링 태스크 시작 (옵션에 따라)
            if (options.EnableMonitoring)
            {
                _ = RunMonitoringAsync(ctoken);
            }

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
        /// Accept 루프
        /// </summary>
        private async Task RunAcceptLoopAsync(CancellationToken ctoken)
        {
            while (!ctoken.IsCancellationRequested)
            {
                TcpClient? conn = null;
                try
                {
                    conn = await server!.AcceptTcpClientAsync(ctoken).ConfigureAwait(false);

                    long newId = Interlocked.Increment(ref connectionIdCounter);

                    if (!isMonitoringEnabled)
                        Log.Information("New connection({RemoteEndPoint}, Id:{NewId}) arrived", conn.Client.RemoteEndPoint, newId);

                    // Socket 설정 최적화
                    conn.NoDelay = true;
                    conn.ReceiveBufferSize = options.ReceiveBufferSize;
                    conn.SendBufferSize = options.SendBufferSize;
                    conn.LingerState = new LingerOption(true, 0);

                    // 팩토리를 통해 작업자 가져오기
                    var worker = workerFactory!.Get(clientPool!);
                    try
                    {
                        if (worker is ClientWorker clientWorker)
                        {
                            clientWorker.SetClient(newId, conn, this.dispatcher!, Sessions, ctoken);
                        }
                        else
                        {
                            worker.SetClient(newId, conn, this.dispatcher!, ctoken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to set client for worker(Id:{ConnId})", newId);
                        workerFactory.Return(clientPool!, worker);
                        throw;
                    }

                    _ = worker.RunReadAsync(clientPool!);
                }
                catch (OperationCanceledException)
                {
                    Log.Fatal("Serve stop signal arrived");

                    if (conn != null)
                    {
                        conn.Dispose();
                        conn = null;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error("Error during accept loop - {Message}", ex.Message);

                    if (conn != null)
                    {
                        conn.Dispose();
                        conn = null;
                    }
                }
            }
        }

        /// <summary>
        /// 모니터링 루프 - CPU, 메모리, 패킷 처리량 등을 주기적으로 로깅
        /// </summary>
        private async Task RunMonitoringAsync(CancellationToken token)
        {
            var process = Process.GetCurrentProcess();
            PerformanceCounter? cpuCounter = InitializeCpuCounter();

            long prevPacketCount = ServerMetrics.TotalPacketsReceived;
            TimeSpan prevCpuTime = process.TotalProcessorTime;
            DateTime prevTime = DateTime.UtcNow;

            int intervalMs = options.MonitoringInterval;
            int historySize = (int)Math.Ceiling(60000.0 / intervalMs);

            var packetHistory = new Queue<long>();
            long packetCountInLastMinute = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, token);

                    var currentTime = DateTime.UtcNow;
                    process.Refresh();
                    var currentCpuTime = process.TotalProcessorTime;
                    var currentPacketCount = ServerMetrics.TotalPacketsReceived;

                    // 프로세스 CPU 사용률 계산
                    double cpuUsedMs = (currentCpuTime - prevCpuTime).TotalMilliseconds;
                    double totalMs = (currentTime - prevTime).TotalMilliseconds;
                    double processCpuPercent = (cpuUsedMs / (totalMs * Environment.ProcessorCount)) * 100;
                    if (processCpuPercent < 0 || double.IsNaN(processCpuPercent))
                        processCpuPercent = 0;

                    // 시스템 CPU 사용률 (Windows 전용)
                    float machineCpu = cpuCounter?.NextValue() ?? 0f;

                    // 패킷 처리량 계산
                    long packetDelta = currentPacketCount - prevPacketCount;

                    // 1분 평균 계산
                    packetHistory.Enqueue(packetDelta);
                    packetCountInLastMinute += packetDelta;
                    if (packetHistory.Count > historySize)
                    {
                        long removed = packetHistory.Dequeue();
                        packetCountInLastMinute -= removed;
                    }
                    double avgPacketPerSecInMin = packetCountInLastMinute / 60.0;

                    // 모니터링 출력이 활성화된 경우에만 로깅
                    if (isMonitoringEnabled)
                    {
                        Log.Information(
                            "CPU(Proc/Sys): {ProcessCpuPercent:F0}/{MachineCpu:F0}%, Mem: {WorkingSetMB}MB, GC(0/1/2): {GC0}/{GC1}/{GC2}, Packets({IntervalMs}ms): {PacketDelta}K, AvgPPS(1m): {AvgPPS:F1}K, Conns: {ActiveConnections}",
                            processCpuPercent,
                            machineCpu,
                            process.WorkingSet64 / 1024 / 1024,
                            GC.CollectionCount(0),
                            GC.CollectionCount(1),
                            GC.CollectionCount(2),
                            intervalMs,
                            packetDelta / 1000,
                            avgPacketPerSecInMin / 1000,
                            ServerMetrics.ActiveConnections);
                    }

                    prevPacketCount = currentPacketCount;
                    prevCpuTime = currentCpuTime;
                    prevTime = currentTime;
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error("Monitor Error: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Windows에서 CPU 성능 카운터를 초기화
        /// </summary>
        private PerformanceCounter? InitializeCpuCounter()
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                counter.NextValue();
                return counter;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to init PerformanceCounter: {Message}", ex.Message);
                return null;
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
