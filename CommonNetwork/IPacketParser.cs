using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 파서 인터페이스
    /// </summary>
    public interface IPacketParser
    {
        /// <summary>
        /// 패킷 읽기
        /// </summary>
        /// <returns></returns>
        public Task<MyPacket> ReadPacket();

        /// <summary>
        /// 패킷 쓰기
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public Task WritePacket(MyPacket packet);

        /// <summary>
        /// 헤더의 Length 사이즈 조회
        /// </summary>
        /// <returns></returns>
        public int GetLengthSize();

        /// <summary>
        /// 헤더의 Type 사이즈 조회
        /// </summary>
        /// <returns></returns>
        public int GetTypeSize();

        /// <summary>
        /// 헤더의 Body를 제외한 사이즈 조회 (Length + Type)
        /// </summary>
        /// <returns></returns>
        public int GetHeaderSize();

        /// <summary>
        /// 패킷의 최대 길이 조회
        /// </summary>
        /// <returns></returns>
        public int GetMaxLength();
    }
}
