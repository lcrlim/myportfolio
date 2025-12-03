using MyCommonNet;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;

public class Program
{
    // 부하 테스트 설정값
    private const int LOAD_TEST_CLIENT_COUNT = 1000;
    private const int LOAD_TEST_REPEAT_COUNT = 1000;
    private const string SERVER_IP = "127.0.0.1";
    private const int SERVER_PORT = 8888;

    private static async Task Main(string[] args)
    {
        // 로거 설정 (필요 시)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        Console.WriteLine("Test tcp client start");
        Console.WriteLine("Press q or quit to exit...");

        // 메인 콘솔용 클라이언트 (커맨드 입력용)
        using (var client = new TestClient())
        {
            Console.WriteLine($"Connecting - {SERVER_IP}:{SERVER_PORT}");
            try
            {
                await client.ConnectAsync(SERVER_IP, SERVER_PORT);
                Console.WriteLine($"Connected - {SERVER_IP}:{SERVER_PORT}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"메인 클라이언트 연결 실패: {ex.Message}");
                return;
            }

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

                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLowerInvariant();
                var cmdArgs = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

                try
                {
                    switch (command)
                    {
                        case "help":
                        case "?":
                            PrintHelp();
                            break;

                        case "ping":
                            await HandlePingCommand(client, cmdArgs);
                            break;

                        case "login":
                            await HandleLoginCommand(client, cmdArgs);
                            break;

                        case "run": // 부하 테스트 명령 추가
                            int repeatCount = 1;
                            if (cmdArgs.Length > 0 && int.TryParse(cmdArgs[0], out int r))
                            {
                                repeatCount = r;
                            }
                            await RunLoadTest(repeatCount);
                            break;

                        default:
                            Console.WriteLine($"알 수 없는 명령어입니다: {command}");
                            Console.WriteLine("help 를 입력하면 사용 가능한 명령어를 볼 수 있습니다.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"명령어 실행 중 오류 발생: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"Test tcp client exit...");
    }

    private static async Task HandlePingCommand(TestClient client, string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("사용법: ping <num> [str]");
            return;
        }

        if (!int.TryParse(args[0], out var pingNum))
        {
            Console.WriteLine("ping 명령어의 첫 번째 인자는 정수여야 합니다. 예) ping 123");
            return;
        }

        string pingStr = args.Length > 1 ? string.Join(' ', args) : args[0];
        Console.WriteLine($"Ping - Num:{pingNum}, Str:{pingStr}");
        var pong = await client.Ping(pingNum, pingStr);
        Console.WriteLine($"Pong - Num:{pong?.Num}, Str:{pong?.Str}");
    }

    private static async Task HandleLoginCommand(TestClient client, string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("사용법: login <userName>");
            return;
        }

        string userName = args[0];
        Console.WriteLine($"Login 요청 - User:{userName}");
        var result = await client.Login(userName);

        if (result == null)
        {
            Console.WriteLine($"Login 실패 - User:{userName}");
        }
        else
        {
            Console.WriteLine($"Login 성공 - User:{userName}, Success:{result.Success}");
        }
    }

    // --- 부하 테스트 로직 ---
    private static async Task RunLoadTest(int loopCount)
    {
        for (int loop = 1; loop <= loopCount; loop++)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine($"부하 테스트 시작 ({loop}/{loopCount}): Client {LOAD_TEST_CLIENT_COUNT}개, 각 {LOAD_TEST_REPEAT_COUNT}회 반복");
            Console.WriteLine("==================================================");

            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();

            // 성공/실패 카운트 (스레드 안전)
            int successCount = 0;
            int errorCount = 0;

            for (int i = 0; i < LOAD_TEST_CLIENT_COUNT; i++)
            {
                int clientId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // 각 태스크마다 새로운 클라이언트 인스턴스 생성
                        using (var loadClient = new TestClient())
                        {
                            // 1. Connect
                            await loadClient.ConnectAsync(SERVER_IP, SERVER_PORT);

                            // 2. Login
                            await loadClient.Login($"User{clientId}");

                            // 3. Ping Loop
                            for (int j = 0; j < LOAD_TEST_REPEAT_COUNT; j++)
                            {
                                await loadClient.Ping(j, "LoadTest");
                            }

                            // 4. Disconnect (using 블록 종료 시 자동 처리되지만 명시적일 수도 있음)
                        }

                        Interlocked.Increment(ref successCount);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref errorCount);
                        // 에러가 너무 많이 출력되면 콘솔이 느려지므로 첫 번째 에러만 출력하거나 로그 레벨 조정
                        //if (errorCount <= 5)
                        //{
                        Console.WriteLine($"ErroCount:{errorCount}, Client {clientId} Error: {ex.Message}");
                        //}
                    }
                }));
            }

            // 모든 클라이언트 작업 완료 대기
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            double totalSeconds = stopwatch.Elapsed.TotalSeconds;
            long totalRequests = (long)successCount * LOAD_TEST_REPEAT_COUNT;

            // 초당 패킷 처리 수 (Ping 1회 + Pong 1회 = 1 Transaction으로 계산 시)
            double tps = totalRequests / totalSeconds;

            Console.WriteLine("==================================================");
            Console.WriteLine($"부하 테스트 완료 ({loop}/{loopCount})");
            Console.WriteLine($"총 소요 시간: {totalSeconds:F3}초");
            Console.WriteLine($"성공 클라이언트: {successCount} / {LOAD_TEST_CLIENT_COUNT}");
            Console.WriteLine($"실패 클라이언트: {errorCount}");
            Console.WriteLine($"총 처리 요청 수 (Ping-Pong): {totalRequests:N0}");
            Console.WriteLine($"평균 Packet Per Sec (TPS): {tps:N2}");
            Console.WriteLine("==================================================");
            
            // 루프 간 잠시 대기 (선택 사항)
            if (loop < loopCount) await Task.Delay(1000);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("---------- Command Help ----------");
        Console.WriteLine("ping <num> [str]   : Ping 패킷 전송. 예) ping 123  /  ping 123 hello");
        Console.WriteLine("login <userName>   : 로그인 패킷 전송(테스트). 예) login aaa");
        Console.WriteLine($"run                : 부하 테스트 실행 ({LOAD_TEST_CLIENT_COUNT} Clients, {LOAD_TEST_REPEAT_COUNT} Pings)");
        Console.WriteLine("help or ?          : 이 도움말 표시");
        Console.WriteLine("q / quit / exit    : 클라이언트 종료");
        Console.WriteLine("----------------------------------");
    }
}