using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            // 로그 초기화
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File($"logs/log_.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            TcpServer server = new TcpServer();

            server.Start(8888);

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

            server.Stop();
            Log.Logger.Information("Tcp server terminated");

            Environment.Exit(0);
        }
    }
}
