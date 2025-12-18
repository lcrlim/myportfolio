using System.Net.Sockets;

namespace MyCommonNet
{
    /// <summary>
    /// 클라이언트 작업자 인터페이스
    /// NetworkStream 및 Pipeline 방식 모두 지원
    /// </summary>
    public interface IClientWorker
    {
        /// <summary>
        /// 클라이언트 설정
        /// </summary>
        /// <param name="connectionId">연결 ID</param>
        /// <param name="conn">TCP 클라이언트</param>
        /// <param name="dispatcher">패킷 디스패처</param>
        /// <param name="ctoken">취소 토큰</param>
        void SetClient(long connectionId, TcpClient conn, IPacketDispatcher dispatcher, CancellationToken ctoken);

        /// <summary>
        /// 읽기 작업 시작
        /// </summary>
        /// <param name="pool">풀 객체</param>
        Task RunReadAsync(object pool);

        /// <summary>
        /// 상태 초기화 (풀 반환 시 호출)
        /// </summary>
        void Reset();
    }
}
