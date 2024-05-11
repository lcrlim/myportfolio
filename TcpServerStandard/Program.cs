using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CommonNetwork;
using Serilog;
using TcpServerStandard;

public class Program
{
    private static async Task Main(string[] args)
    {
        // 로그 초기화
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File($"logs/log_.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        int port = 8888;
        if (args.Length > 0)
        {
            // 임의로 1번째는 포트
            int.TryParse(args[0], out port);
        }

        TcpServer server = new TcpServer();

        var cts = new CancellationTokenSource();
        var startedTask = server.Start(port, new PacketDispatcher(), cts.Token);

        var process = Process.GetCurrentProcess();

        Log.Logger.Information($"Tcp server start {process.ProcessName}(PID:{process.Id}) - Port:{port}");

        while (true)
        {
            Log.Logger.Information("Press q or quit to exit...");
            var str = Console.ReadLine();
            if (str?.ToLower() == "q" || str?.ToLower() == "quit" || str?.ToLower() == "exit")
            {
                Log.Logger.Information("Process terminate by console command");
                break;
            }
        }

        cts.Cancel();
        await startedTask;
        Log.Logger.Information("Tcp server terminated");

        Environment.Exit(0);
    }
}

























