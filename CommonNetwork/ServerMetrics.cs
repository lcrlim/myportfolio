using System.Runtime.InteropServices;
using System.Threading;

namespace MyCommonNet
{
    /// <summary>
    /// 서버 메트릭 - False Sharing 방지 (128바이트 패딩)
    /// </summary>
    public static class ServerMetrics
    {
        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct MetricData
        {
            [FieldOffset(0)]
            public long TotalPacketsReceived;

            // 128 bytes cache line padding
            [FieldOffset(128)]
            public int ActiveConnections;
        }

        private static MetricData _data;

        public static long TotalPacketsReceived => Interlocked.Read(ref _data.TotalPacketsReceived);
        public static int ActiveConnections => _data.ActiveConnections;

        public static void IncrementPacketCount()
        {
            Interlocked.Increment(ref _data.TotalPacketsReceived);
        }

        public static void IncrementConnectionCount()
        {
            Interlocked.Increment(ref _data.ActiveConnections);
        }

        public static void DecrementConnectionCount()
        {
            Interlocked.Decrement(ref _data.ActiveConnections);
        }
    }
}
