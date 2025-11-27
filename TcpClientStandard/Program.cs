using MyCommonNet;
using Serilog;
using System;
using System.Threading.Tasks;

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

        // IP 입력
        while (true)
        {
            Console.Write("IP : ");
            ip = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                var lower = ip.Trim().ToLowerInvariant();
                if (lower == "q" || lower == "quit" || lower == "exit")
                {
                    quit = true;
                }
                break;
            }
        }

        // Port 입력
        while (!quit)
        {
            Console.Write("Port : ");
            strPort = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(strPort))
            {
                var lower = strPort.Trim().ToLowerInvariant();
                if (lower == "q" || lower == "quit" || lower == "exit")
                {
                    quit = true;
                    break;
                }

                if (int.TryParse(strPort, out port))
                    break;

                Console.WriteLine("Port 는 숫자로 입력해 주세요.");
            }
        }

        if (!quit && !string.IsNullOrWhiteSpace(ip))
        {
            using (var client = new TestClient())
            {
                Console.WriteLine($"Connecting - {ip}:{port}");
                await client.ConnectAsync(ip, port);
                Console.WriteLine($"Connected - {ip}:{port}");

                // 처음 연결되면 help 한 번 보여주기
                PrintHelp();

                while (true)
                {
                    Console.Write("> ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    var trimmed = input.Trim();
                    var lower = trimmed.ToLowerInvariant();

                    if (lower == "q" || lower == "quit" || lower == "exit")
                    {
                        Log.Logger.Information("Process terminate by console command");
                        break;
                    }

                    // 명령어 파싱: command arg1 arg2 ...
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var command = parts[0].ToLowerInvariant();
                    var cmdArgs = parts.Length > 1
                        ? parts[1..]
                        : Array.Empty<string>();

                    try
                    {
                        switch (command)
                        {
                            case "help":
                            case "?":
                                {
                                    PrintHelp();
                                    break;
                                }

                            case "ping":
                                {
                                    // ping 123  or  ping 123 hello
                                    if (cmdArgs.Length < 1)
                                    {
                                        Console.WriteLine("사용법: ping <num> [str]");
                                        break;
                                    }

                                    if (!int.TryParse(cmdArgs[0], out var pingNum))
                                    {
                                        Console.WriteLine("ping 명령어의 첫 번째 인자는 정수여야 합니다. 예) ping 123");
                                        break;
                                    }

                                    // 문자열 인자는 없으면 "123" 그대로 사용
                                    string pingStr = cmdArgs.Length > 1
                                        ? string.Join(' ', cmdArgs)
                                        : cmdArgs[0];

                                    Console.WriteLine($"Ping - Num:{pingNum}, Str:{pingStr}");
                                    var pong = await client.Ping(pingNum, pingStr);
                                    Console.WriteLine($"Pong - Num:{pong?.Num}, Str:{pong?.Str}");
                                    break;
                                }

                            case "login":
                                {
                                    // login aaa
                                    if (cmdArgs.Length < 1)
                                    {
                                        Console.WriteLine("사용법: login <userName>");
                                        break;
                                    }

                                    string userName = cmdArgs[0];

                                    Console.WriteLine($"Login 요청 - User:{userName}");

                                    // ⚠️ TestClient 에 아래 시그니처의 메서드를 구현해 주세요.
                                    // public Task<LoginResult?> Login(string userName);
                                    var result = await client.Login(userName);

                                    if (result == null)
                                    {
                                        Console.WriteLine($"Login 실패 - User:{userName}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Login 성공 - User:{userName}, Success:{result.Success}");
                                    }

                                    break;
                                }

                            default:
                                {
                                    Console.WriteLine($"알 수 없는 명령어입니다: {command}");
                                    Console.WriteLine("help 를 입력하면 사용 가능한 명령어를 볼 수 있습니다.");
                                    break;
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"명령어 실행 중 오류 발생: {ex.Message}");
                    }
                }
            }
        }

        Console.WriteLine($"Test tcp client exit...");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("---------- Command Help ----------");
        Console.WriteLine("ping <num> [str]   : Ping 패킷 전송. 예) ping 123  /  ping 123 hello");
        Console.WriteLine("login <userName>   : 로그인 패킷 전송(테스트). 예) login aaa");
        Console.WriteLine("help or ?          : 이 도움말 표시");
        Console.WriteLine("q / quit / exit    : 클라이언트 종료");
        Console.WriteLine("----------------------------------");
    }
}
