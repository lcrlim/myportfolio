using Microsoft.Extensions.Configuration;
using MyCommonNet;

namespace TcpServerStandard
{
    /// <summary>
    /// 서버 설정 로드 및 명령행 인수 파싱
    /// </summary>
    public static class ServerConfiguration
    {
        public static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
        }

        /// <summary>
        /// 명령행 인수를 파싱하여 TcpServerOptions에 적용
        /// 사용법: TcpServerStandard.exe [port] [--pipeline|--netstream]
        /// 예: TcpServerStandard.exe 8888 --pipeline
        /// </summary>
        /// <param name="args">명령행 인수</param>
        /// <param name="options">적용할 옵션 객체</param>
        public static void ApplyCommandLineArguments(string[] args, TcpServerOptions options)
        {
            foreach (var arg in args)
            {
                if (arg.Equals("--pipeline", StringComparison.OrdinalIgnoreCase))
                {
                    options.UsePipeline = true;
                }
                else if (arg.Equals("--netstream", StringComparison.OrdinalIgnoreCase))
                {
                    options.UsePipeline = false;
                }
                else if (int.TryParse(arg, out int port))
                {
                    options.Port = port;
                }
            }
        }
    }
}
