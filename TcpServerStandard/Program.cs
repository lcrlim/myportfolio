using Microsoft.Extensions.DependencyInjection;
using MyCommonNet;
using Serilog;
using Serilog.Filters;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TcpServerStandard;

public class Program
{
    private static volatile bool isMonitoring = true;   // 실행 시 모니터링 켜진 상태로 시작

    private static async Task Main(string[] args)
    {
        // 콘솔 출력용 확장 메서드 적용
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File("logs/log-.txt", 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}")
            .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(evt => evt.Properties.ContainsKey("ConsoleOutput"))
            .WriteTo.Console())
            .CreateLogger();

        try
        {
            int port = 8888;
            if (args.Length > 0)
            {
                // 임의로 1번째는 포트
                _ = int.TryParse(args[0], out port);
            }

            // -------------------------
            // DI 컨테이너 구성
            // -------------------------
            var services = new ServiceCollection();

            // Serilog Logger 주입 (원하면 사용)
            services.AddSingleton(Log.Logger);

            // 핸들러들이 들어있는 어셈블리 (예: PacketPingHandler 정의된 어셈블리)
            var handlerAssembly = typeof(PacketPingHandler).Assembly;

            // IPacketHandler<T> 구현 클래스들을 한 번에 등록
            services.AddPacketHandlersFromAssembly(handlerAssembly);

            // PacketDispatcher 등록
            services.AddSingleton<IPacketDispatcher>(sp =>
                new PacketDispatcher(sp, handlerAssembly));

            // ServiceProvider 생성
            var serviceProvider = services.BuildServiceProvider();

            // Dispatcher 꺼내기
            var dispatcher = serviceProvider.GetRequiredService<IPacketDispatcher>();

            // -------------------------
            // TCP 서버 시작
            // -------------------------
            TcpServer server = new TcpServer();

            var cts = new CancellationTokenSource();
            var startedTask = server.Start(port, dispatcher, cts.Token);

            var process = Process.GetCurrentProcess();
            Log.Logger.ToConsole().Information("Tcp server start {ProcessName}(PID:{ProcessId}) - Port:{Port}", process.ProcessName, process.Id, port);

            // Start Monitoring Task
            _ = MonitorAsync(cts.Token);

            Log.Logger.ToConsole().Information("Press 'q' or type 'quit' to exit...");
            Log.Logger.ToConsole().Information("Press 'm' to toggle server monitor logging.");

            StringBuilder inputBuffer = new StringBuilder();

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    
                    // 'm' toggle logic (only when buffer is empty or we treat it as hotkey)
                    // To avoid conflict if user types "me" for some reason, we treat 'm' as hotkey ONLY if buffer is empty? 
                    // Or just always? Requirement says "Press m key". Let's assume hotkey.
                    if (keyInfo.Key == ConsoleKey.M && inputBuffer.Length == 0)
                    {
                        isMonitoring = !isMonitoring;
                        Console.WriteLine($"\n[System] Monitor Output: {(isMonitoring ? "ON" : "OFF")}");
                        continue;
                    }

                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine(); // New line
                        var input = inputBuffer.ToString().Trim().ToLowerInvariant();
                        inputBuffer.Clear();

                        if (input == "q" || input == "quit" || input == "exit")
                        {
                            Log.Logger.ToConsole().Information("Process terminate by console command");
                            break;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (inputBuffer.Length > 0)
                        {
                            inputBuffer.Length--;
                            Console.Write("\b \b");
                        }
                    }
                    else
                    {
                        inputBuffer.Append(keyInfo.KeyChar);
                        Console.Write(keyInfo.KeyChar);
                    }
                }
                else
                {
                    await Task.Delay(50);
                }
            }

            cts.Cancel();
            await startedTask.ConfigureAwait(false);
            Log.Logger.ToConsole().Information("Tcp server terminated");
        }
        catch (Exception ex)
        {
            Log.Logger.ToConsole().Fatal(ex, "Fatal error in server main loop.");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        Environment.Exit(0);
    }

    private static async Task MonitorAsync(CancellationToken token)
    {
        var process = Process.GetCurrentProcess();
        // PerformanceCounter is Windows specific
        PerformanceCounter? cpuCounter = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call always returns 0
            }
            catch (Exception ex)
            {
                Log.Logger.Error("Failed to init PerformanceCounter: {Message}", ex.Message);
            }
        }

        long prevPacketCount = ServerMetrics.TotalPacketsReceived;
        TimeSpan prevCpuTime = process.TotalProcessorTime;
        DateTime prevTime = DateTime.UtcNow;

        // 1분간의 패킷 수신량을 저장할 큐 (2초 간격이므로 30개)
        Queue<long> packetHistory = new Queue<long>();
        long packetCountInLastMinute = 0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, token);

                var currentTime = DateTime.UtcNow;
                process.Refresh(); // Refresh process info
                var currentCpuTime = process.TotalProcessorTime;
                var currentPacketCount = ServerMetrics.TotalPacketsReceived;

                double cpuUsedMs = (currentCpuTime - prevCpuTime).TotalMilliseconds;
                double totalMs = (currentTime - prevTime).TotalMilliseconds;
                // 코어 수로 나누어서 전체 시스템 대비 프로세스 점유율 계산 (0~100%)
                // 만약 코어당 점유율(0~100 * Core)을 원하면 Environment.ProcessorCount를 제거
                double processCpuPercent = (cpuUsedMs / (totalMs * Environment.ProcessorCount)) * 100;
                if (processCpuPercent < 0) processCpuPercent = 0;
                if (double.IsNaN(processCpuPercent)) processCpuPercent = 0;

                float machineCpu = 0f;
                if (cpuCounter != null)
                {
                    machineCpu = cpuCounter.NextValue();
                }

                long packetDelta = currentPacketCount - prevPacketCount;

                // 1분 평균 계산
                packetHistory.Enqueue(packetDelta);
                packetCountInLastMinute += packetDelta;
                if (packetHistory.Count > 30)
                {
                    long removed = packetHistory.Dequeue();
                    packetCountInLastMinute -= removed;
                }
                double avgPacketPerSecInMin = packetCountInLastMinute / 60.0;

                if (isMonitoring)
                {
                    Log.Logger.ToConsole().Information("CPU(Proc/Sys): {ProcessCpuPercent:F0}/{MachineCpu:F0}%, Mem: {WorkingSetMB}MB, GC(0/1/2): {GC0}/{GC1}/{GC2}, Packets(2s): {PacketDelta}K, AvgPPS(1m): {AvgPPS:F1}K, Conns: {ActiveConnections}",
                        processCpuPercent, machineCpu, process.WorkingSet64 / 1024 / 1024, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), packetDelta/1000, avgPacketPerSecInMin/1000, ServerMetrics.ActiveConnections);
                }

                prevPacketCount = currentPacketCount;
                prevCpuTime = currentCpuTime;
                prevTime = currentTime;
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                Log.Logger.Error("Monitor Error: {Message}", ex.Message);
            }
        }
    }
}


























