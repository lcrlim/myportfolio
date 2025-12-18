using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// System.IO.Pipelines 기반 패킷 파서
    /// 고성능 비동기 I/O를 위한 구현
    /// SemaphoreSlim으로 동시 전송 보호 + 버퍼 재사용으로 Zero-allocation
    /// MyPacketPool을 사용하여 패킷 객체를 재사용.
    /// </summary>
    public class PipelinePacketParser : IPacketParser, IDisposable
    {
        private readonly int _lengthSize;
        private readonly int _typeSize;
        private readonly int _maxLength;
        private readonly int _headerSize;
        private readonly int _minimumPipeBufferSize;

        // 동시 전송 보호용 Semaphore
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        // 내부 파이프 및 태스크 관리
        private Pipe? _pipe;
        private Task? _fillPipeTask;
        private CancellationTokenSource? _cts;
        private Socket? _socket;

        // Reader는 내부 파이프의 것을 참조
        private PipeReader? _reader;

        // 송신용 버퍼 재사용 (SemaphoreSlim으로 보호됨)
        private readonly byte[] _sendHeaderBuffer;
        private readonly List<ArraySegment<byte>> _sendSegments;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="options">TCP 서버 옵션 (null인 경우 기본값 사용)</param>
        /// <param name="lengthSize">패킷 길이 필드 크기</param>
        /// <param name="typeSize">패킷 타입 필드 크기</param>
        /// <param name="maxLength">최대 패킷 크기</param>
        public PipelinePacketParser(
            TcpServerOptions? options = null,
            int lengthSize = Packet.PACKET_HEADER_LEN_SIZE,
            int typeSize = Packet.PACKET_HEADER_TYPE_SIZE,
            int maxLength = Packet.PACKET_MAX_SIZE)
        {
            this._lengthSize = lengthSize;
            this._typeSize = typeSize;
            this._maxLength = maxLength;
            this._headerSize = lengthSize + typeSize;

            // 옵션에서 버퍼 크기 가져오기 (기본값: 8192)
            this._minimumPipeBufferSize = options?.MinimumPipeBufferSize ?? 8192;

            // 송신용 버퍼 사전 할당 (재사용)
            _sendHeaderBuffer = new byte[_headerSize];
            _sendSegments = new List<ArraySegment<byte>>(2);
        }

        /// <summary>
        /// Socket 설정 및 데이터 수신 루프 시작
        /// </summary>
        public void SetSocket(Socket socket)
        {
            ResetStream(); // 이전 리소스 정리

            this._socket = socket;
            this._cts = new CancellationTokenSource();

            // 파이프 생성 (기본 옵션 사용 + ThreadPool)
            this._pipe = new Pipe(new PipeOptions(
                pool: MemoryPool<byte>.Shared,
                readerScheduler: PipeScheduler.ThreadPool,
                writerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false));

            this._reader = _pipe.Reader;

            // 백그라운드에서 소켓 -> 파이프 데이터 펌핑 시작
            this._fillPipeTask = FillPipeAsync(socket, _pipe.Writer, _cts.Token);
        }

        /// <summary>
        /// 소켓으로부터 데이터를 읽어 파이프에 씀
        /// </summary>
        private async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 파이프에서 쓰기 가능한 메모리 확보
                    Memory<byte> memory = writer.GetMemory(_minimumPipeBufferSize);

                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, token).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        break; // 연결 종료
                    }

                    // 데이터가 쓰여졌음을 알림
                    writer.Advance(bytesRead);

                    // Reader가 읽을 수 있도록 Flush
                    FlushResult result = await writer.FlushAsync(token).ConfigureAwait(false);

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소 처리
            }
            catch (Exception)
            {
                // 소켓 에러 등 발생 시 루프 종료
            }
            finally
            {
                // Writer 종료
                await writer.CompleteAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 리소스 초기화 및 백그라운드 작업 중단
        /// </summary>
        public void ResetStream()
        {
            _cts?.Cancel(); // 데이터 수신 중단 요청

            // 파이프 정리
            if (_pipe != null)
            {
                _pipe.Reader.Complete();
                _pipe.Writer.Complete();
            }

            // 실행 중인 태스크가 있다면 안전하게 종료되도록 보장 (Fire-and-forget)
            if (_fillPipeTask != null)
            {
                _ = WaitForTaskCompletion(_fillPipeTask);
            }

            this._socket = null;
            this._cts?.Dispose();
            this._cts = null;
            this._pipe = null;
            this._reader = null;
            this._fillPipeTask = null;
        }

        private async Task WaitForTaskCompletion(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        public int GetLengthSize() => _lengthSize;
        public int GetTypeSize() => _typeSize;
        public int GetHeaderSize() => _headerSize;
        public int GetMaxLength() => _maxLength;

        /// <summary>
        /// Pipeline 방식으로 패킷 읽기
        /// </summary>
        public async Task<MyPacket> ReadPacket()
        {
            if (_reader == null)
                throw new InvalidOperationException("PipeReader is null.");

            int packetLength = 0;
            int packetType = 0;

            while (true)
            {
                ReadResult result = await _reader.ReadAsync().ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (result.IsCanceled)
                    throw new OperationCanceledException();

                // 연결 종료 처리 (데이터도 없고 Writer도 완료됨)
                if (result.IsCompleted && buffer.Length == 0)
                    throw new ObjectDisposedException("connection closed");

                // 헤더 사이즈 부족
                if (buffer.Length < _headerSize)
                {
                    if (result.IsCompleted)
                        throw new ObjectDisposedException("connection closed - incomplete header");

                    // 부족하니 더 읽기 위해 커서 이동
                    _reader.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                // 헤더 사이즈 이상 도착했으니 파싱
                if (packetLength == 0 || packetType == 0)
                    ParseHeader(buffer.Slice(0, _headerSize), out packetLength, out packetType);

                // 유효성 검사
                if (packetLength > _maxLength || packetLength < _headerSize)
                    throw new ArgumentOutOfRangeException($"Invalid packet size: {packetLength}");

                // 전체 패킷이 아직 도착하지 않음
                if (buffer.Length < packetLength)
                {
                    if (result.IsCompleted)
                        throw new ObjectDisposedException("connection closed - incomplete packet");

                    // 나머지 바디를 읽기위해 커서 이동
                    _reader.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                // 바디 추출
                int bodySize = packetLength - _headerSize;
                ReadOnlyMemory<byte> bodyMemory = ReadOnlyMemory<byte>.Empty;

                byte[]? rentedArray = null;
                try
                {
                    if (bodySize > 0)
                    {
                        ReadOnlySequence<byte> bodySlice = buffer.Slice(_headerSize, bodySize);
                        rentedArray = ArrayPool<byte>.Shared.Rent(bodySize);

                        bodySlice.CopyTo(rentedArray);
                        bodyMemory = rentedArray.AsMemory(0, bodySize);
                    }

                    // 버퍼 소비 완료 처리
                    _reader.AdvanceTo(buffer.GetPosition(packetLength));

                    // MyPacketPool 사용
                    var packet = MyPacketPool.Rent();
                    packet.Len = packetLength;
                    packet.Type = packetType;
                    packet.BodyMemory = bodyMemory;
                    packet._rentedBuffer = rentedArray;

                    return packet;
                }
                catch
                {
                    if (rentedArray != null)
                        ArrayPool<byte>.Shared.Return(rentedArray);
                    throw;
                }
            }
        }

        /// <summary>
        /// 헤더 파싱
        /// </summary>
        private void ParseHeader(ReadOnlySequence<byte> headerSlice, out int packetLength, out int packetType)
        {
            if (headerSlice.IsSingleSegment)
            {
                // 단일 세그멘트로 왔으면 바로 읽고
                ReadOnlySpan<byte> span = headerSlice.FirstSpan;
                packetLength = BinaryPrimitives.ReadInt32LittleEndian(span);
                packetType = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(_lengthSize));
            }
            else
            {
                // 멀티 세그멘트면 SequenceReader 사용
                var reader = new SequenceReader<byte>(headerSlice);
                reader.TryReadLittleEndian(out packetLength);
                reader.TryReadLittleEndian(out packetType);
            }
        }

        /// <summary>
        /// 패킷을 송신 큐에 추가 (브로드캐스트용)
        /// 실제 전송은 WritePacket으로 직접 수행
        /// </summary>
        public ValueTask EnqueuePacketAsync(MyPacket packet, CancellationToken cancellationToken = default)
        {
            // 브로드캐스트 시나리오: WritePacket을 직접 호출
            return new ValueTask(WritePacket(packet));
        }

        /// <summary>
        /// 패킷 전송 (Semaphore로 동시 접근 보호)
        /// </summary>
        public async Task WritePacket(MyPacket packet)
        {
            if (_socket == null) return;

            await _sendLock.WaitAsync().ConfigureAwait(false);

            byte[]? rentedBodyBytes = null;
            int bodyLength = 0;

            try
            {
                _sendSegments.Clear();

                // Body 준비
                if (!packet.BodyMemory.IsEmpty)
                {
                    bodyLength = packet.BodyMemory.Length;

                    // ArrayPool 사용 복사
                    rentedBodyBytes = ArrayPool<byte>.Shared.Rent(bodyLength);
                    packet.BodyMemory.CopyTo(rentedBodyBytes);
                    _sendSegments.Add(new ArraySegment<byte>(rentedBodyBytes, 0, bodyLength));
                }

                // Header 준비
                int totalLen = _headerSize + bodyLength;
                packet.Len = totalLen;

                BinaryPrimitives.WriteInt32LittleEndian(_sendHeaderBuffer, packet.Len);
                BinaryPrimitives.WriteInt32LittleEndian(_sendHeaderBuffer.AsSpan(_lengthSize), packet.Type);

                // 헤더를 리스트의 맨 앞에 삽입
                _sendSegments.Insert(0, new ArraySegment<byte>(_sendHeaderBuffer));

                // 전송 (Scatter/Gather)
                await _socket.SendAsync(_sendSegments, SocketFlags.None).ConfigureAwait(false);
            }
            finally
            {
                if (rentedBodyBytes != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedBodyBytes);
                }
                _sendSegments.Clear();
                _sendLock.Release();
            }
        }

        /// <summary>
        /// 송신 루프 시작 (브로드캐스트 전용 - 현재 미사용)
        /// </summary>
        public Task StartSendLoopAsync(CancellationToken cancellationToken)
        {
            // 요청-응답 패턴에서는 사용하지 않음
            return Task.CompletedTask;
        }

        /// <summary>
        /// 송신 큐 종료 신호 (브로드캐스트 전용 - 현재 미사용)
        /// </summary>
        public void CompleteSendQueue()
        {
            // 요청-응답 패턴에서는 사용하지 않음
        }

        /// <summary>
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
            ResetStream();
            _sendLock.Dispose();
        }
    }
}
