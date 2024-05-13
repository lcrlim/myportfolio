using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThrottleApiServer
{
    [Serializable]
    public class ThrottleCounterInDb
    {
        public DateTime CreatedTime { get; set; }
        public long Val {  get; set; }
        public DateTime ExpiresAtTime { get; set; }
        public DateTime? OldCreatedTime { get; set; }
        public long? OldVal { get; set; }
        public DateTime? OldExpiresAtTime { get; set; }

        public bool IsNewCounter()
        {
            return (OldCreatedTime != null && CreatedTime != OldCreatedTime);
        }
    }
}
