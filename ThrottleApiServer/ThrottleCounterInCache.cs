using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThrottleApiServer
{
    [Serializable]
    internal struct ThrottleCounterInCache
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan ExpirationTime { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int TempCount { get; set; }
        public bool Synchronizing { get; set; }
       
        public long SynchronizedCount { get; set; }
        public long TotalCount
        {
            get
            {
                return SynchronizedCount + TempCount;
            }
        }

        public int GetToSyncCount(int rate, DateTime now)
        {
            int count = 0;
            if (!Synchronizing)
            {
                if (TempCount % rate == 0 || now > ExpirationDate)
                {
                    count = TempCount;
                }
            }
            return count;
        }

        public int GetToSyncCountWithNolock(int rate, DateTime now)
        {
            if (TempCount % rate == 0 || now > ExpirationDate)
            {
                return TempCount;
            }
            return 0;

        }
    }
}
