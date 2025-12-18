using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using MyCommonNet;
using MyCommonNet.Serialization;

namespace TcpClientStandard
{
    /// <summary>
    /// 독립적인 TCP 네트워크 클라이언트
    /// CommonNetwork.TestClient를 대체하여 더 나은 성능과 유연성 제공
    /// </summary>
    public class TcpNetworkClient : IDisposable
    {
        private TcpClient? _client;
        private IPacketParser? _parser;
        private readonly ISerializer _serializer;
        private readonly TcpClientOptions _options;
        private bool _disposed;

        /// <summary>
        /// 연결 상태
        /// </summary>
        public bool IsConnected => _client?.Connected ?? false;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="options">클라이언트 설정 옵션 (null이면 기본값 사용)</param>
        public TcpNetworkClient(TcpClientOptions? options = null)
        {
            _options = options ?? new TcpClientOptions();
            _serializer = SerializerFactory.Create(_options.SerializerType);
            _disposed = false;
        }

        /// <summary>
        /// 서버에 연결
        /// </summary>
        /// <param name="host">서버 호스트 (옵션에서 지정된 값 사용 시 null)</param>
        /// <param name="port">서버 포트 (옵션에서 지정된 값 사용 시 0)</param>
        public async Task ConnectAsync(string? host = null, int port = 0)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpNetworkClient));

            if (IsConnected)
                throw new InvalidOperationException("이미 연결된 상태입니다.");

            var targetHost = host ?? _options.Host;
            var targetPort = port > 0 ? port : _options.Port;

            _client = new TcpClient();

            // 소켓 버퍼 크기 설정
            _client.ReceiveBufferSize = _options.ReceiveBufferSize;
            _client.SendBufferSize = _options.SendBufferSize;

            // 연결 (타임아웃 적용)
            using var cts = new CancellationTokenSource(_options.ConnectTimeoutMs);
            try
            {
                await _client.ConnectAsync(targetHost, targetPort, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"서버 연결 타임아웃: {targetHost}:{targetPort}");
            }

            // Parser 초기화
            var parserOptions = new TcpServerOptions
            {
                FixedBufferSize = _options.FixedBufferSize
            };

            if (_options.UsePipeline)
            {
                _parser = new PipelinePacketParser(parserOptions);
                ((PipelinePacketParser)_parser).SetSocket(_client.Client);
            }
            else
            {
                _parser = new PacketParser(parserOptions);
                ((PacketParser)_parser).SetStream(_client.GetStream());
            }
        }

        /// <summary>
        /// 서버 연결 종료
        /// </summary>
        public void Disconnect()
        {
            if (_client != null)
            {
                try
                {
                    _parser?.ResetStream();
                    _client.Close();
                }
                catch
                {
                    // 무시
                }
                _client = null;
                _parser = null;
            }
        }

        /// <summary>
        /// 제네릭 요청-응답 메서드
        /// </summary>
        /// <typeparam name="TRequest">요청 타입</typeparam>
        /// <typeparam name="TResponse">응답 타입</typeparam>
        /// <param name="request">요청 객체</param>
        /// <param name="packetType">패킷 타입 코드</param>
        /// <returns>응답 객체</returns>
        public async Task<TResponse?> SendAsync<TRequest, TResponse>(TRequest request, int packetType)
            where TRequest : class
            where TResponse : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpNetworkClient));

            if (!IsConnected || _parser == null)
                throw new InvalidOperationException("서버에 연결되지 않았습니다.");

            // 요청 패킷 풀에서 가져오기
            var packet = MyPacketPool.Rent();
            try
            {
                packet.Type = packetType;

                // 요청 직렬화
                var buffer = packet.EnsureWriteBuffer(_serializer.GetMaxSize(request));
                int written = _serializer.Serialize(request, buffer);
                packet.WriteBufferLength = written;
                packet.BodyMemory = packet.WriteBuffer.Slice(0, written);
                packet.Len = Packet.PACKET_HEADER_SIZE + written;

                // 요청 전송
                await _parser.WritePacket(packet).ConfigureAwait(false);
            }
            finally
            {
                MyPacketPool.Return(packet);
            }

            // 응답 수신
            var response = await _parser.ReadPacket().ConfigureAwait(false);
            try
            {
                if (!response.BodyMemory.IsEmpty)
                {
                    return _serializer.Deserialize<TResponse>(response.BodyMemory);
                }
                return default;
            }
            finally
            {
                MyPacketPool.Return(response);
            }
        }

        /// <summary>
        /// 패킷만 전송 (응답 없음)
        /// </summary>
        /// <typeparam name="TRequest">요청 타입</typeparam>
        /// <param name="request">요청 객체</param>
        /// <param name="packetType">패킷 타입 코드</param>
        public async Task SendOnlyAsync<TRequest>(TRequest request, int packetType)
            where TRequest : class
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpNetworkClient));

            if (!IsConnected || _parser == null)
                throw new InvalidOperationException("서버에 연결되지 않았습니다.");

            var packet = MyPacketPool.Rent();
            try
            {
                packet.Type = packetType;

                var buffer = packet.EnsureWriteBuffer(_serializer.GetMaxSize(request));
                int written = _serializer.Serialize(request, buffer);
                packet.WriteBufferLength = written;
                packet.BodyMemory = packet.WriteBuffer.Slice(0, written);
                packet.Len = Packet.PACKET_HEADER_SIZE + written;

                await _parser.WritePacket(packet).ConfigureAwait(false);
            }
            finally
            {
                MyPacketPool.Return(packet);
            }
        }

        // ===== 기존 호환 메서드 =====

        /// <summary>
        /// PING 요청 전송
        /// </summary>
        /// <param name="num">테스트 번호</param>
        /// <param name="str">테스트 문자열</param>
        /// <returns>PONG 응답</returns>
        public async Task<PacketPong?> Ping(int num, string str)
        {
            var request = new PacketPing { Num = num, Str = str };
            return await SendAsync<PacketPing, PacketPong>(request, (int)Packet.PacketType.PING).ConfigureAwait(false);
        }

        /// <summary>
        /// 로그인 요청 전송
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <returns>로그인 결과</returns>
        public async Task<PacketLoginResult?> Login(string userId)
        {
            var request = new PacketLogin { UserId = userId };
            return await SendAsync<PacketLogin, PacketLoginResult>(request, (int)Packet.PacketType.LOGIN).ConfigureAwait(false);
        }

        /// <summary>
        /// 캐릭터 목록 요청 전송
        /// </summary>
        /// <param name="userId">사용자 ID</param>
        /// <returns>캐릭터 목록</returns>
        public async Task<PacketCharacterListResult?> Characters(string userId)
        {
            var request = new PacketCharacterList { UserId = userId };
            return await SendAsync<PacketCharacterList, PacketCharacterListResult>(request, (int)Packet.PacketType.CHARACTER_LIST).ConfigureAwait(false);
        }

        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            Disconnect();
            _disposed = true;
        }
    }
}
