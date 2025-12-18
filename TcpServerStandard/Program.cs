using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyCommonNet;
using Serilog;
using System.Diagnostics;
using TcpServerStandard;

public class Program
{
    private static async Task Main(string[] args)
    {
        // 로깅 설정
        LoggingSetup.ConfigureLogging();

        try
        {
            // 설정 로드
            var configuration = ServerConfiguration.BuildConfiguration();
            var options = new TcpServerOptions();
            configuration.GetSection("TcpServer").Bind(options);
            ServerConfiguration.ApplyCommandLineArguments(args, options);

            // DI 컨테이너 구성
            var serviceProvider = DependencyInjectionSetup.ConfigureServices(options);
            var dispatcher = serviceProvider.GetRequiredService<IPacketDispatcher>();

            // TCP 서버 시작 (모니터링 자동 시작됨)
            var server = new TcpServer(options);
            var cts = new CancellationTokenSource();
            var serverTask = server.Start(options.Port, dispatcher, cts.Token);

            LogServerStartInfo(options);

            // 콘솔 입력 처리
            await RunConsoleInputLoopAsync(server);

            // 서버 종료
            cts.Cancel();
            await serverTask.ConfigureAwait(false);
            Log.Information("Tcp server terminated.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error in server main loop.");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        Environment.Exit(0);
    }

    /// <summary>
    /// 서버 시작 정보 로깅
    /// </summary>
    private static void LogServerStartInfo(TcpServerOptions options)
    {
        var process = Process.GetCurrentProcess();
        string mode = options.UsePipeline ? "Pipeline" : "NetworkStream";
        Log.Information(
            "Tcp server start {ProcessName}(PID:{ProcessId}) - Port:{Port}, Mode:{Mode}",
            process.ProcessName, process.Id, options.Port, mode);
    }

    /// <summary>
    /// 콘솔 입력 루프
    /// </summary>
    private static async Task RunConsoleInputLoopAsync(TcpServer server)
    {
        ConsoleInputHandler.PrintUsage();
        var inputHandler = new ConsoleInputHandler();

        while (true)
        {
            bool shouldExit = inputHandler.ProcessInput(() => server.ToggleMonitor());

            if (shouldExit) break;

            await Task.Delay(100);
        }
    }
}
