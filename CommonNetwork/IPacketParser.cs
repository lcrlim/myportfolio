using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 파서 인터페이스
    /// Channel 기반 비동기 송신 큐를 지원.
    /// </summary>
    public interface IPacketParser
    {
        /// <summary>
        /// 패킷 읽기
        /// </summary>
        /// <returns></returns>
        public Task<MyPacket> ReadPacket();

        /// <summary>
        /// 패킷을 송신 큐에 추가 (비동기, 논블로킹)
        /// 패킷은 SendLoop에서 전송 후 자동으로 풀에 반환됩니다.
        /// 서버에서 사용 권장
        /// </summary>
        /// <param name="packet">전송할 패킷</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns></returns>
        public ValueTask EnqueuePacketAsync(MyPacket packet, CancellationToken cancellationToken = default);

        /// <summary>
        /// 패킷을 직접 전송 (동기적 전송)
        /// 클라이언트에서 사용 권장
        /// </summary>
        /// <param name="packet">전송할 패킷</param>
        /// <returns></returns>
        public Task WritePacket(MyPacket packet);

        /// <summary>
        /// 송신 루프 시작 (백그라운드 Task로 실행)
        /// 큐에서 패킷을 꺼내 순차적으로 전송.
        /// </summary>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns></returns>
        public Task StartSendLoopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 송신 큐 종료 신호
        /// 더 이상 패킷을 추가하지 않음을 알립니다.
        /// </summary>
        public void CompleteSendQueue();

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

        /// <summary>
        /// 스트림 또는 소켓 리소스 초기화
        /// </summary>
        public void ResetStream();
    }
}
