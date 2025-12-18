using Serilog;
using System.Text;

namespace TcpServerStandard
{
    /// <summary>
    /// 콘솔 입력 처리를 담당
    /// q/quit/exit: 서버 종료
    /// m: 모니터링 토글
    /// </summary>
    public class ConsoleInputHandler
    {
        private readonly StringBuilder inputBuffer = new();

        /// <summary>
        /// 콘솔 입력을 처리
        /// </summary>
        /// <param name="onToggleMonitor">모니터링 토글 시 호출될 콜백</param>
        /// <returns>종료 요청 시 true, 계속 실행 시 false</returns>
        public bool ProcessInput(Action onToggleMonitor)
        {
            if (!Console.KeyAvailable)
                return false;

            var keyInfo = Console.ReadKey(intercept: true);

            // 'm' 키 처리: 버퍼가 비어있을 때만 모니터링 토글
            if (keyInfo.Key == ConsoleKey.M && inputBuffer.Length == 0)
            {
                onToggleMonitor();
                return false;
            }

            // Enter 키 처리: 명령어 실행
            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                var input = inputBuffer.ToString().Trim().ToLowerInvariant();
                inputBuffer.Clear();

                if (IsExitCommand(input))
                {
                    Log.Information("Process terminate by console command");
                    return true;
                }

                return false;
            }

            // Backspace 키 처리
            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (inputBuffer.Length > 0)
                {
                    inputBuffer.Length--;
                    Console.Write("\b \b");
                }
                return false;
            }

            // 일반 문자 입력
            inputBuffer.Append(keyInfo.KeyChar);
            Console.Write(keyInfo.KeyChar);
            return false;
        }

        /// <summary>
        /// 종료 명령어인지 확인
        /// </summary>
        private static bool IsExitCommand(string input)
        {
            return input == "q" || input == "quit" || input == "exit";
        }

        /// <summary>
        /// 사용법 안내 메시지를 출력
        /// </summary>
        public static void PrintUsage()
        {
            Log.Information("Press 'q' or type 'quit' to exit...");
            Log.Information("Press 'm' to toggle server monitor logging.");
        }
    }
}
