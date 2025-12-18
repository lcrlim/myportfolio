using Microsoft.Extensions.ObjectPool;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// MyPacket 객체 풀링을 위한 정적 클래스
    /// GC 부하를 줄이기 위해 요청/응답 패킷 객체를 재사용.
    /// </summary>
    public static class MyPacketPool
    {
        private static ObjectPool<MyPacket>? _pool;
        private static bool _initialized = false;

        /// <summary>
        /// 패킷 풀 초기화
        /// 서버 시작 전에 호출하여 풀 크기를 설정.
        /// </summary>
        /// <param name="maxRetained">최대로 유지할 패킷 객체 수</param>
        public static void Initialize(int maxRetained = 10000)
        {
            if (_initialized)
            {
                Log.Warning("MyPacketPool already initialized. Ignoring duplicate initialization.");
                return;
            }

            var provider = new DefaultObjectPoolProvider();
            provider.MaximumRetained = maxRetained;

            _pool = provider.Create(new PacketPooledObjectPolicy());
            _initialized = true;
        }

        public static MyPacket Rent()
        {
            if (_pool == null)
                throw new InvalidOperationException("MyPacketPool is not initialized.");

            return _pool.Get();
        }

        public static void Return(MyPacket packet)
        {
            if (_pool == null)
                throw new InvalidOperationException("MyPacketPool is not initialized.");

            _pool.Return(packet);
        }

        private class PacketPooledObjectPolicy : IPooledObjectPolicy<MyPacket>
        {
            public MyPacket Create()
            {
                return new MyPacket();
            }

            public bool Return(MyPacket obj)
            {
                obj.Reset();
                return true;
            }
        }
    }
}
