using Serilog;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace MyCommonNet
{
    /// <summary>
    /// 통합 클라이언트 작업자
    /// IPacketParser를 주입받아 NetworkStream 또는 Pipeline 방식을 모두 지원.
    /// 패킷 풀링을 통해 GC 압력을 최소화.
    /// </summary>
    public class ClientWorker : IClientWorker
    {
        private readonly IPacketParser _parser;
        private long _connId;
        private TcpClient? _client;
        private CancellationToken _cancellationToken;
        private IPacketDispatcher? _dispatcher;
        private SessionManager? _sessionManager;

        /// <summary>
        /// 연결 ID (외부에서 참조 가능)
        /// </summary>
        public long ConnectionId => _connId;

        /// <summary>
        /// 패킷 파서 (외부에서 EnqueuePacketAsync 호출 가능)
        /// </summary>
        public IPacketParser Parser => _parser;

        /// <summary>
        /// 생성자 (풀링용)
        /// </summary>
        /// <param name="parser">패킷 파서 (NetworkStream 또는 Pipeline 방식)</param>
        public ClientWorker(IPacketParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <summary>
        /// 클라이언트 연결 설정
        /// </summary>
        public void SetClient(long connectionId, TcpClient conn, IPacketDispatcher dispatcher, CancellationToken ctoken)
        {
            SetClient(connectionId, conn, dispatcher, null, ctoken);
        }

        /// <summary>
        /// 클라이언트 연결 설정 (SessionManager 포함)
        /// </summary>
        public void SetClient(long connectionId, TcpClient conn, IPacketDispatcher dispatcher, SessionManager? sessionManager, CancellationToken ctoken)
        {
            _connId = connectionId;
            _client = conn;
            _cancellationToken = ctoken;
            _dispatcher = dispatcher;
            _sessionManager = sessionManager;

            // Parser 타입에 따라 적절한 설정 메서드 호출
            if (_parser is PacketParser packetParser)
            {
                // NetworkStream 방식
                packetParser.SetStream(conn.GetStream());
            }
            else if (_parser is PipelinePacketParser pipelineParser)
            {
                // Pipeline 방식
                pipelineParser.SetSocket(conn.Client);
            }
            else
            {
                throw new NotSupportedException($"Unsupported parser type: {_parser.GetType().Name}");
            }

            // SessionManager에 등록
            _sessionManager?.Register(_connId, _parser);
        }

        /// <summary>
        /// 상태 초기화 (풀 반환 시 호출)
        /// </summary>
        public void Reset()
        {
            // SessionManager에서 해제
            _sessionManager?.Unregister(_connId);

            if (_client != null)
            {
                try
                {
                    _client.Dispose();
                }
                catch
                {
                }
            }
            _client = null;
            _connId = 0;
            _cancellationToken = default;
            _dispatcher = null;
            _sessionManager = null;

            try
            {
                _parser.ResetStream();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error resetting parser stream in ClientWorker");
            }
        }

        /// <summary>
        /// 비동기 읽기 루프
        /// 패킷을 읽고 디스패치한 후 응답을 전송.
        /// 패킷 객체는 MyPacketPool을 통해 재사용됩니다.
        /// </summary>
        public async Task RunReadAsync(object poolObj)
        {
            var pool = (ObjectPool<ClientWorker>)poolObj;
            if (_client == null || _dispatcher == null)
            {
                Log.Error("Client or dispatcher is null in ClientWorker(Id:{ConnId})", _connId);
                pool.Return(this);
                return;
            }

            bool added = false;
            try
            {
                ServerMetrics.IncrementConnectionCount();
                added = true;

                // 패킷 읽기 루프
                while (!_cancellationToken.IsCancellationRequested)
                {
                    MyPacket req = await _parser.ReadPacket().ConfigureAwait(false);

                    ServerMetrics.IncrementPacketCount();

                    try
                    {
                        // 패킷 처리 (비동기)
                        MyPacket? res = await _dispatcher.DispatchAsync(req, _cancellationToken).ConfigureAwait(false);

                        // 응답 전송 (직접 전송 - 요청-응답 패턴에 최적)
                        if (res != null)
                        {
                            try
                            {
                                await _parser.WritePacket(res).ConfigureAwait(false);
                            }
                            finally
                            {
                                MyPacketPool.Return(res);
                            }
                        }
                    }
                    finally
                    {
                        // 요청 패킷 처리 후에 풀 반환
                        MyPacketPool.Return(req);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (!TcpServer.IsMonitoring)
                    Log.Information("Connection(Id:{ConnId}) cancelled", _connId);
            }
            catch (ObjectDisposedException)
            {
                if (!TcpServer.IsMonitoring)
                    Log.Information("Connection(Id:{ConnId}) closed", _connId);
            }
            catch (SocketException ex)
            {
                if (!TcpServer.IsMonitoring)
                    Log.Information("Connection(Id:{ConnId}) closed by socket error - {ErrorCode}", _connId, ex.SocketErrorCode);
            }
            catch (Exception ex)
            {
                // 정상적인 종료가 아닌 경우
                Log.Warning("Connection(Id:{ConnId}) closed by error - {Message}", _connId, ex.Message);
            }
            finally
            {
                if (added)
                {
                    ServerMetrics.DecrementConnectionCount();
                }
                pool.Return(this);
            }
        }
    }
}
