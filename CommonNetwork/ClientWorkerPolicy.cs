using Microsoft.Extensions.ObjectPool;
using Serilog;
using System;

namespace MyCommonNet
{
    /// <summary>
    /// ClientWorker 객체 풀링 정책
    /// Parser 팩토리 함수를 받아 각 Worker에 적절한 Parser를 생성.
    /// </summary>
    public class ClientWorkerPolicy : IPooledObjectPolicy<ClientWorker>
    {
        private readonly Func<IPacketParser> _parserFactory;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="parserFactory">Parser 생성 팩토리 함수</param>
        public ClientWorkerPolicy(Func<IPacketParser> parserFactory)
        {
            _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
        }

        /// <summary>
        /// 풀에 객체가 없을 때 새로 생성하는 로직
        /// </summary>
        public ClientWorker Create()
        {
            Log.Debug("ClientWorker created");
            return new ClientWorker(_parserFactory());
        }

        /// <summary>
        /// 풀에 반환될 때 호출되는 로직
        /// </summary>
        public bool Return(ClientWorker obj)
        {
            Log.Debug("ClientWorker returned");
            obj.Reset(); // 상태 초기화
            return true; // true를 반환해야 풀에 다시 들어갑니다.
        }
    }
}
