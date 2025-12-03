using System.Threading;

namespace MyCommonNet
{
    public static class ServerMetrics
    {
        private static long _totalPacketsReceived;
        private static int _activeConnections;

        public static long TotalPacketsReceived => Interlocked.Read(ref _totalPacketsReceived);
        public static int ActiveConnections => _activeConnections;

        public static void IncrementPacketCount()
        {
            Interlocked.Increment(ref _totalPacketsReceived);
        }

        public static void IncrementConnectionCount()
        {
            Interlocked.Increment(ref _activeConnections);
        }

        public static void DecrementConnectionCount()
        {
            Interlocked.Decrement(ref _activeConnections);
        }
    }
}
