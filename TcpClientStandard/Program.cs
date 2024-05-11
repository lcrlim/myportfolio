using CommonNetwork;
using Serilog;
using System.Net.NetworkInformation;
using System.Net.Quic;

public class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Test tcp client start");
        Console.WriteLine("Press q or quit to exit...");

        string? ip = null;
        string? strPort = null;
        int port = 0;
        bool quit = false;
        while (true)
        {
            Console.WriteLine("IP : ");
            ip = Console.ReadLine();
            if (!string.IsNullOrEmpty(ip))
            {
                if (ip?.ToLower() == "q" || ip?.ToLower() == "quit" || ip?.ToLower() == "exit")
                {
                    quit = true;
                }
                break;
            }
        }

        while (true)
        {
            Console.WriteLine("Port : ");
            strPort = Console.ReadLine();
            if (!string.IsNullOrEmpty(strPort))
            {
                if(int.TryParse(strPort, out port))
                    break;

                if (strPort?.ToLower() == "q" || strPort?.ToLower() == "quit" || strPort?.ToLower() == "exit")
                {
                    quit = true;
                    break;
                }
            }
        }

        if (!quit)
        {
            using (var client = new TestClient())
            {
                Console.WriteLine($"Connecting - {ip}:{port}");
                await client.ConnectAsync(ip, port);
                Console.WriteLine($"Connected - {ip}:{port}");

                while (true)
                {
                    var str = Console.ReadLine();
                    if (str?.ToLower() == "q" || str?.ToLower() == "quit" || str?.ToLower() == "exit")
                    {
                        Log.Logger.Information("Process terminate by console command");
                        break;
                    }
                    else
                    {
                        switch (str)
                        {
                            case "ping":
                                {
                                    int pingNum = 123;
                                    string pingStr = "test123";
                                    Console.WriteLine($"Ping - Num:{pingNum}, Str:{pingStr}");
                                    var pong = await client.Ping(pingNum, pingStr);
                                    Console.WriteLine($"Pong - Num:{pong.Num}, Str:{pong.Str}");
                                    break;
                                }
                            default:
                                {
                                    break;
                                }
                        }
                    }
                }
            }
        }

        Console.WriteLine($"Test tcp client exit...");
    }
}