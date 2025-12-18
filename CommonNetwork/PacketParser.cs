using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 수신한 요청 패킷을 역직렬화 해서 객체로 리턴하고
    /// 송신할 응답 패킷을 직렬화해서 바이트 배열로 리턴
    /// Zero Allocation 적용하여 GC 부하를 최소화
    /// 패킷 크기가 설정된 버퍼 크기 이하일 경우 고정 버퍼를 사용하고, 큰 사이즈는 ArrayPool 사용
    /// MyPacketPool을 사용하여 패킷 객체를 재사용.
    /// </summary>
    public class PacketParser : IPacketParser, IDisposable
    {
        private readonly int _lengthSize = 0;
        private readonly int _typeSize = 0;
        private readonly int _maxLength = 0;
        private readonly int _headerSize;

        /// <summary>
        /// 고정 버퍼 크기 (옵션에서 설정 가능)
        /// </summary>
        private readonly int _fixedBufferSize;

        /// <summary>
        /// 작은 버퍼 복사 여부 (false: 더블 버퍼링으로 Zero-copy, true: 기존 복사 방식)
        /// </summary>
        private readonly bool _copySmallBuffers;

        /// <summary>
        /// 헤더 읽기 전용 고정 버퍼
        /// </summary>
        private byte[] _headerReadBuffer;

        /// <summary>
        /// 수신용 고정 버퍼 1 (더블 버퍼링)
        /// </summary>
        private byte[] _fixedReadBuffer1;

        /// <summary>
        /// 수신용 고정 버퍼 2 (더블 버퍼링)
        /// </summary>
        private byte[] _fixedReadBuffer2;

        /// <summary>
        /// 현재 사용 중인 읽기 버퍼 인덱스 (0 또는 1)
        /// </summary>
        private int _currentBufferIndex = 0;

        /// <summary>
        /// 송신용 고정 버퍼
        /// </summary>
        private byte[] _fixedWriteBuffer;

        /// <summary>
        /// 클라이언트와 연결된 네트워크 스트림
        /// </summary>
        private NetworkStream? _stream;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="options">TCP 서버 옵션 (null인 경우 기본값 사용)</param>
        /// <param name="lengthSize">패킷 길이 필드의 크기 (바이트)</param>
        /// <param name="typeSize">패킷 타입 필드의 크기 (바이트)</param>
        /// <param name="maxLength">허용할 최대 패킷 크기 (바이트)</param>
        public PacketParser(
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
            this._fixedBufferSize = options?.FixedBufferSize ?? 8192;

            // 옵션에서 복사 여부 가져오기 (기본값: false = 더블 버퍼링 사용)
            this._copySmallBuffers = options?.CopySmallBuffers ?? false;

            // 헤더 읽기용 버퍼 별도 할당
            this._headerReadBuffer = new byte[_headerSize];

            // 읽기용 고정 버퍼 할당 (더블 버퍼링)
            this._fixedReadBuffer1 = new byte[_fixedBufferSize];
            this._fixedReadBuffer2 = new byte[_fixedBufferSize];

            // 쓰기용 고정 버퍼 할당
            this._fixedWriteBuffer = new byte[_fixedBufferSize];

            this._stream = null;
        }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="stream">연결된 네트워크 스트림</param>
        /// <param name="options">TCP 서버 옵션</param>
        /// <param name="lengthSize">패킷 길이 필드의 크기</param>
        /// <param name="typeSize">패킷 타입 필드의 크기</param>
        /// <param name="maxLength">최대 패킷 크기</param>
        public PacketParser(
            NetworkStream stream,
            TcpServerOptions? options = null,
            int lengthSize = Packet.PACKET_HEADER_LEN_SIZE,
            int typeSize = Packet.PACKET_HEADER_TYPE_SIZE,
            int maxLength = Packet.PACKET_MAX_SIZE)
            : this(options, lengthSize, typeSize, maxLength)
        {
            this._stream = stream;
        }

        /// <summary>
        /// 네트워크 스트림을 설정
        /// </summary>
        public void SetStream(NetworkStream stream) => this._stream = stream;

        /// <summary>
        /// 네트워크 스트림 설정을 초기화
        /// </summary>
        public void ResetStream() => this._stream = null;

        /// <summary>
        /// 현재 설정된 네트워크 스트림 반환
        /// </summary>
        public NetworkStream? GetStream() => this._stream;

        /// <summary>
        /// 헤더의 길이 필드 크기를 반환
        /// </summary>
        public int GetLengthSize() => _lengthSize;

        /// <summary>
        /// 헤더의 타입 필드 크기를 반환
        /// </summary>
        public int GetTypeSize() => _typeSize;

        /// <summary>
        /// 헤더 전체 크기를 반환 (길이 필드 + 타입 필드)
        /// </summary>
        public int GetHeaderSize() => GetTypeSize() + GetLengthSize();

        /// <summary>
        /// 패킷의 최대 크기를 반환
        /// </summary>
        public int GetMaxLength() => _maxLength;

        /// <summary>
        /// 네트워크 스트림에서 패킷 읽기
        /// </summary>
        /// <returns>수신된 패킷 객체 (MyPacket)</returns>
        public virtual async Task<MyPacket> ReadPacket()
        {
            NetworkStream? stream = GetStream();
            if (stream == null)
            {
                throw new Exception("Network stream is null");
            }

            int headerSize = GetHeaderSize();

            // 헤더 읽기
            try
            {
                await stream.ReadExactlyAsync(_headerReadBuffer.AsMemory(0, headerSize)).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                throw new ObjectDisposedException("network stream");
            }

            // 2. 헤더 파싱 (메모리 할당 없이 변환)
            ReadOnlySpan<byte> headerSpan = _headerReadBuffer.AsSpan(0, headerSize);
            int packetLength = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(0, this._lengthSize));
            int packetType = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(this._lengthSize, this._typeSize));

            // 패킷 크기 유효성 검사
            if (packetLength > GetMaxLength() || packetLength < headerSize)
            {
                throw new ArgumentOutOfRangeException($"Invalid Packet size: {packetLength}");
            }

            // 3. 바디 데이터 읽기
            int bodySize = packetLength - headerSize;

            // 패킷 객체를 풀에서 가져오기 (재사용)
            MyPacket packet = MyPacketPool.Rent();
            packet.Len = packetLength;
            packet.Type = packetType;

            if (bodySize > 0)
            {
                byte[]? rentedBuffer = null;
                Memory<byte> targetBuffer;

                // 바디 크기가 고정 버퍼 크기를 초과하면 ArrayPool 사용
                if (bodySize > _fixedBufferSize)
                {
                    rentedBuffer = ArrayPool<byte>.Shared.Rent(bodySize);
                    targetBuffer = rentedBuffer.AsMemory(0, bodySize);
                }
                else
                {
                    // 고정 버퍼 크기 이하일 경우 더블 버퍼링 사용
                    // 현재 버퍼 선택 (교차 사용)
                    var currentBuffer = _currentBufferIndex == 0 ? _fixedReadBuffer1 : _fixedReadBuffer2;
                    _currentBufferIndex = 1 - _currentBufferIndex; // 토글
                    targetBuffer = currentBuffer.AsMemory(0, bodySize);
                }

                try
                {
                    // 바디 데이터 수신
                    await stream.ReadExactlyAsync(targetBuffer).ConfigureAwait(false);

                    if (rentedBuffer == null)
                    {
                        if (_copySmallBuffers)
                        {
                            // 복사 모드: 기존 방식처럼 데이터를 별도 배열로 복사
                            // 비동기 핸들러가 오래 걸리는 경우 안전함
                            byte[] bodyCopy = new byte[bodySize];
                            targetBuffer.Span.CopyTo(bodyCopy);
                            packet.BodyMemory = bodyCopy;
                            packet._rentedBuffer = null;
                        }
                        else
                        {
                            // 더블 버퍼링 모드: 복사 없이 직접 참조 (Zero-copy)
                            // 다음 패킷 읽기 전에 현재 패킷 처리가 완료되어야 함
                            // 더블 버퍼링으로 한 번의 여유를 제공
                            packet.BodyMemory = targetBuffer;
                            packet._rentedBuffer = null;
                        }
                    }
                    else
                    {
                        // ArrayPool 사용 시: Zero-copy 달성
                        // 렌트 버퍼를 패킷에 저장하여 나중에 자동 반환
                        packet.BodyMemory = targetBuffer;
                        packet._rentedBuffer = rentedBuffer;
                    }
                }
                catch (EndOfStreamException)
                {
                    // 예외 발생 시 즉시 반환
                    if (rentedBuffer != null) ArrayPool<byte>.Shared.Return(rentedBuffer);
                    MyPacketPool.Return(packet);
                    throw new ObjectDisposedException("network stream");
                }
            }
            else
            {
                // 바디가 없는 경우
                packet.BodyMemory = ReadOnlyMemory<byte>.Empty;
                packet._rentedBuffer = null;
            }

            return packet;
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
        /// 패킷을 네트워크 스트림으로 전송
        /// </summary>
        /// <param name="packet">전송할 패킷 객체</param>
        public virtual async Task WritePacket(MyPacket packet)
        {
            var stream = GetStream();
            if (stream != null)
            {
                int bodyByteCount = 0;

                bodyByteCount = packet.BodyMemory.Length;

                int totalSize = GetHeaderSize() + bodyByteCount;

                byte[]? rentedBuffer = null;
                Memory<byte> bufferMemory;
                Span<byte> bufferSpan;

                // 전송할 데이터가 고정 버퍼 크기를 초과하면 ArrayPool 사용
                if (totalSize > _fixedBufferSize)
                {
                    rentedBuffer = ArrayPool<byte>.Shared.Rent(totalSize);
                    bufferMemory = rentedBuffer.AsMemory(0, totalSize);
                    bufferSpan = rentedBuffer.AsSpan(0, totalSize);
                }
                else
                {
                    // 고정 버퍼 크기 이하일 경우 고정 버퍼 사용
                    bufferMemory = _fixedWriteBuffer.AsMemory(0, totalSize);
                    bufferSpan = _fixedWriteBuffer.AsSpan(0, totalSize);
                }

                try
                {
                    packet.Len = totalSize;

                    // 1. 헤더 쓰기
                    BinaryPrimitives.WriteInt32LittleEndian(bufferSpan.Slice(0, this._lengthSize), packet.Len);
                    BinaryPrimitives.WriteInt32LittleEndian(bufferSpan.Slice(this._lengthSize, this._typeSize), packet.Type);

                    // 2. 바디 쓰기
                    if (bodyByteCount > 0)
                    {
                        // 메모리에서 직접 복사
                        packet.BodyMemory.Span.CopyTo(bufferSpan.Slice(GetHeaderSize(), bodyByteCount));
                    }

                    // 3. 데이터 전송
                    await stream.WriteAsync(bufferMemory).ConfigureAwait(false);
                }
                finally
                {
                    // 대여한 버퍼가 있다면 반납
                    if (rentedBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
                }
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
        }
    }
}
