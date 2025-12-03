using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MyCommonNet
{
    // 풀링 정책 정의
    public class ClientWorkerPolicy : IPooledObjectPolicy<ClientWorker>
    {
        // 풀에 객체가 없을 때 새로 생성하는 로직
        public ClientWorker Create()
        {
            Log.Logger.Debug("ClientWorker created");
            return new ClientWorker();
        }

        // 풀에 반환될 때 호출되는 로직
        public bool Return(ClientWorker obj)
        {
            Log.Logger.Debug("ClientWorker returned");
            obj.Reset(); // 상태 초기화
            return true; // true를 반환해야 풀에 다시 들어갑니다.
        }
    }
}
