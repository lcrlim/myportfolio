using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 헤더
    /// </summary>
    public class MyPacket
    {
        /// <summary>
        /// 패킷 총 길이, 4바이트, Len 항목의 길이도 포함
        /// </summary>
        public int Len { get; set; }
        /// <summary>
        /// 패킷의 타입, 4바이트
        /// </summary>
        public int Type { get; set; }
        /// <summary>
        /// 패킷의 실제 데이터, json string으로 전달 (utf8)
        /// </summary>
        public string? Body { get; set; }
    }
}
