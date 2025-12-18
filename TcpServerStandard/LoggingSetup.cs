using Serilog;

namespace TcpServerStandard
{
    /// <summary>
    /// Serilog 로깅 설정을 담당
    /// </summary>
    public static class LoggingSetup
    {
        /// <summary>
        /// Serilog 로거를 구성
        /// 파일 로깅 (일별 롤링) 및 콘솔 출력을 설정
        /// </summary>
        public static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File("logs/log-.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}")
                .WriteTo.Console()
                .CreateLogger();
        }
    }
}
