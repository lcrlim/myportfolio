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
        /// 레거시 호환성을 위해 남겨둠. 가능하면 BodyMemory를 사용하세요.
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// 패킷 데이터의 메모리 뷰 (Zero Allocation용)
        /// </summary>
        public ReadOnlyMemory<byte> BodyMemory { get; set; }
    }
}
