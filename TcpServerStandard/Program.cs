using Microsoft.Extensions.DependencyInjection;
using MyCommonNet;
using Serilog;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TcpServerStandard;

public class Program
{
    private static async Task Main(string[] args)
    {
        //    // 로그 초기화
        //    Log.Logger = new LoggerConfiguration()
        //        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}")
        //        .WriteTo.File($"logs/log_.txt", rollingInterval: RollingInterval.Day,
        //            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}")
        //        .Enrich.WithThreadId()
        //        .CreateLogger();

        //    int port = 8888;
        //    if (args.Length > 0)
        //    {
        //        // 임의로 1번째는 포트
        //        int.TryParse(args[0], out port);
        //    }

        //    TcpServer server = new TcpServer();

        //    var cts = new CancellationTokenSource();
        //    var startedTask = server.Start(port, new PacketDispatcher(), cts.Token);

        //    var process = Process.GetCurrentProcess();

        //    Log.Logger.Information($"Tcp server start {process.ProcessName}(PID:{process.Id}) - Port:{port}");

        //    while (true)
        //    {
        //        Log.Logger.Information("Press q or quit to exit...");
        //        var str = Console.ReadLine();
        //        if (str?.ToLower() == "q" || str?.ToLower() == "quit" || str?.ToLower() == "exit")
        //        {
        //            Log.Logger.Information("Process terminate by console command");
        //            break;
        //        }
        //    }

        //    cts.Cancel();
        //    await startedTask;
        //    Log.Logger.Information("Tcp server terminated");

        //    Environment.Exit(0);

        // 로그 초기화 (기존 그대로)
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}")
            .WriteTo.File("logs/log_.txt",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}")
            .Enrich.WithThreadId()
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

            // 외부 의존 서비스들 등록 (예제)
            //services.AddSingleton<IFakeExternalService, FakeExternalService>();

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
            Log.Logger.Information($"Tcp server start {process.ProcessName}(PID:{process.Id}) - Port:{port}");

            while (true)
            {
                Log.Logger.Information("Press q or quit to exit...");
                var str = Console.ReadLine();
                if (str is not null)
                {
                    var lower = str.ToLowerInvariant();
                    if (lower is "q" or "quit" or "exit")
                    {
                        Log.Logger.Information("Process terminate by console command");
                        break;
                    }
                }
            }

            cts.Cancel();
            await startedTask.ConfigureAwait(false);
            Log.Logger.Information("Tcp server terminated");
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Fatal error in server main loop.");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        Environment.Exit(0);
    }
}

























