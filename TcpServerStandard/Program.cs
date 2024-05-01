using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

        TcpServer server = new TcpServer();

        var cts = new CancellationTokenSource();
        var startedTask = server.Start(8888, cts.Token);

        var process = Process.GetCurrentProcess();

        Log.Logger.Information($"Tcp server start {process.ProcessName}(PID:{process.Id})");

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

























