using MyCommonNet.Serialization;

namespace MyCommonNet
{
    /// <summary>
    /// TCP 서버 설정 옵션
    /// </summary>
    public class TcpServerOptions
    {
        /// <summary>
        /// 서버 리스닝 포트
        /// </summary>
        public int Port { get; set; } = 8888;

        /// <summary>
        /// Pipeline 방식 사용 여부 (true: Pipeline, false: NetworkStream)
        /// </summary>
        public bool UsePipeline { get; set; } = false;

        /// <summary>
        /// 클라이언트 작업자 풀 최대 크기
        /// </summary>
        public int MaxPoolSize { get; set; } = 10000;

        /// <summary>
        /// 소켓 수신 버퍼 크기
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// 소켓 송신 버퍼 크기
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;

        // ===== 버퍼 설정 =====

        /// <summary>
        /// 고정 버퍼 크기 (바이트)
        /// 이 크기 이하의 패킷은 고정 버퍼를 사용하여 ArrayPool 사용 안함.
        /// </summary>
        public int FixedBufferSize { get; set; } = 8192;

        /// <summary>
        /// 작은 버퍼 복사 여부
        /// false (기본값): 더블 버퍼링으로 Zero-copy 달성
        /// true: 기존 복사 방식 (비동기 핸들러가 오래 걸리는 경우 안전)
        /// </summary>
        public bool CopySmallBuffers { get; set; } = false;

        /// <summary>
        /// Pipeline 최소 버퍼 크기 (바이트)
        /// System.IO.Pipelines 사용 시 적용됩니다.
        /// </summary>
        public int MinimumPipeBufferSize { get; set; } = 512;

        /// <summary>
        /// ArrayPool 최소 크기 (바이트)
        /// 이 크기 이상의 패킷에 대해 ArrayPool을 사용.
        /// </summary>
        public int ArrayPoolMinimumSize { get; set; } = 8192;

        // ===== 직렬화 설정 =====

        /// <summary>
        /// 직렬화 방식 선택
        /// Json (기본값): System.Text.Json 사용 (호환성 우선)
        /// MessagePack: 고성능 바이너리 직렬화 (성능 우선)
        /// </summary>
        public SerializerType SerializerType { get; set; } = SerializerType.Json;

        // ===== 풀 설정 =====

        /// <summary>
        /// 패킷 객체 풀 최대 크기
        /// MyPacketPool에서 최대로 유지할 패킷 객체 수
        /// </summary>
        public int PacketPoolSize { get; set; } = 10000;

        /// <summary>
        /// 작업자 풀 최대 크기
        /// ClientWorker 풀에서 최대로 유지할 작업자 수
        /// </summary>
        public int WorkerPoolSize { get; set; } = 10000;

        // ===== 모니터링 설정 =====

        /// <summary>
        /// 모니터링 활성화 여부
        /// true: 서버 시작 시 모니터링 태스크 자동 시작
        /// </summary>
        public bool EnableMonitoring { get; set; } = true;

        /// <summary>
        /// 모니터링 간격 (밀리초)
        /// 서버 메트릭을 출력하는 간격
        /// </summary>
        public int MonitoringInterval { get; set; } = 1000;

        // ===== 송신 큐 설정 =====

        /// <summary>
        /// 클라이언트당 송신 큐 최대 크기
        /// Backpressure를 위해 큐가 가득 차면 대기.
        /// </summary>
        public int SendQueueCapacity { get; set; } = 1000;
    }
}
