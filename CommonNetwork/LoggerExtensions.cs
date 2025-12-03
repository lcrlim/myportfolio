using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using System.Text.Json.Serialization;
using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.ObjectPool;

namespace MyCommonNet
{
    public static class LoggerExtensions
    {
        // 특정 로그는 콘솔+파일 둘다 출력하고자 할때 사용할 확장 메서드
        public static ILogger ToConsole(this ILogger logger)
        {
            return logger.ForContext("ConsoleOutput", true);
        }
    }
}
