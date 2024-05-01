using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerStandard
{
    public class ProcessManager
    {
        public static readonly ProcessManager Instance = new ProcessManager();


        public void Start()
        {
            var process = Process.GetCurrentProcess();

            Log.Logger.Information($"Start {process.ProcessName}(PID:{process.Id}) as console mode");

            while(true)
            {
                Log.Logger.Information("Press q or quit to exit...");
                var str = Console.ReadLine();
                if (str?.ToLower() == "q" || str?.ToLower() == "quit" || str?.ToLower() == "exit")
                {
                    Log.Logger.Information("Process terminate by console command");
                    return;
                }
            }            
        }
    }
}
