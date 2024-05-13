using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThrottleApiServer
{
    /// <summary>
    /// throttle counter 테이블
    /// </summary>
    [Table("ApiThrottleCounter")]
    public class ApiThrottleCounter
    {
        public string Id { get; set; }
        public long Val { get; set; }
        public long AddVal { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ExpiresAtTime { get; set; }
        public long SlidingExpirationInSeconds { get; set; }
        public DateTime AbsoluteExpiration {  get; set; }
    }
}
