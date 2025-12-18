using Microsoft.Extensions.ObjectPool;
using Serilog;
using System;
using System.Collections.Generic;

namespace MyCommonNet
{
    /// <summary>
    /// 클라이언트 작업자 팩토리 인터페이스
    /// </summary>
    public interface IClientWorkerFactory
    {
        /// <summary>
        /// 클라이언트 작업자 풀 생성
        /// </summary>
        object CreatePool(int maxSize);

        /// <summary>
        /// 풀에서 작업자 가져오기
        /// </summary>
        IClientWorker Get(object pool);

        /// <summary>
        /// 작업자를 풀에 반환
        /// </summary>
        void Return(object pool, IClientWorker worker);
    }

    /// <summary>
    /// 통합 클라이언트 작업자 팩토리
    /// Parser 팩토리 함수를 받아 NetworkStream 또는 Pipeline 방식을 지원.
    /// </summary>
    public class ClientWorkerFactory : IClientWorkerFactory
    {
        private readonly Func<IPacketParser> _parserFactory;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="parserFactory">Parser 생성 팩토리 함수</param>
        public ClientWorkerFactory(Func<IPacketParser> parserFactory)
        {
            _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
        }

        /// <summary>
        /// 클라이언트 작업자 풀 생성
        /// </summary>
        public object CreatePool(int maxSize)
        {
            var provider = new DefaultObjectPoolProvider { MaximumRetained = maxSize };
            var pool = provider.Create(new ClientWorkerPolicy(_parserFactory));

            Log.Information("Client worker pool created");

            var typedPool = (ObjectPool<ClientWorker>)pool;

            for (int i = 0; i < maxSize; i++)
            {
                typedPool.Return(typedPool.Get());
            }

            Log.Information("Client worker pool warmed up - {Count}", maxSize);

            return pool;
        }

        /// <summary>
        /// 풀에서 작업자 가져오기
        /// </summary>
        public IClientWorker Get(object pool)
        {
            var typedPool = (ObjectPool<ClientWorker>)pool;
            return typedPool.Get();
        }

        /// <summary>
        /// 작업자를 풀에 반환
        /// </summary>
        public void Return(object pool, IClientWorker worker)
        {
            var typedPool = (ObjectPool<ClientWorker>)pool;
            typedPool.Return((ClientWorker)worker);
        }
    }
}
