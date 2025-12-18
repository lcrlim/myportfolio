using MyCommonNet.Serialization;

namespace TcpClientStandard
{
    /// <summary>
    /// TCP 클라이언트 설정 옵션
    /// </summary>
    public class TcpClientOptions
    {
        /// <summary>
        /// 서버 호스트 주소
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// 서버 포트
        /// </summary>
        public int Port { get; set; } = 8888;

        /// <summary>
        /// Pipeline 방식 사용 여부 (true: Pipeline, false: NetworkStream)
        /// </summary>
        public bool UsePipeline { get; set; } = false;

        /// <summary>
        /// 직렬화 방식 선택
        /// Json (기본값): System.Text.Json 사용 (호환성 우선)
        /// MessagePack: 고성능 바이너리 직렬화 (성능 우선)
        /// </summary>
        public SerializerType SerializerType { get; set; } = SerializerType.Json;

        /// <summary>
        /// 소켓 수신 버퍼 크기
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// 소켓 송신 버퍼 크기
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        /// <summary>
        /// 고정 버퍼 크기 (바이트)
        /// </summary>
        public int FixedBufferSize { get; set; } = 8192;

        /// <summary>
        /// 연결 타임아웃 (밀리초)
        /// </summary>
        public int ConnectTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// 읽기 타임아웃 (밀리초, 0이면 무한)
        /// </summary>
        public int ReadTimeoutMs { get; set; } = 0;

        /// <summary>
        /// 쓰기 타임아웃 (밀리초, 0이면 무한)
        /// </summary>
        public int WriteTimeoutMs { get; set; } = 0;
    }
}
