using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 수신한 요청 패킷을 역직렬화 해서 객체로 리턴하고 
    /// 송신할 응답 패킷을 직렬화해서 바이트 배열로 리턴
    /// Zero Allocation 적용하여 GC 부하를 최소화
    /// 패킷 크기가 8KB 이하일 경우 고정 버퍼를 사용하고, 큰 사이즈는 ArrayPool 사용 후 반납
    /// </summary>
    public class PacketParser : IPacketParser, IDisposable
    {
        private int lengthSize = 0;
        private int typeSize = 0;
        private int maxLength = 0;

        /// <summary>
        /// 고정 버퍼 크기
        /// </summary>
        private const int FIXED_BUFFER_SIZE = 8192;

        /// <summary>
        /// 헤더 읽기 전용 고정 버퍼
        /// </summary>
        private byte[] headerReadBuffer;

        /// <summary>
        /// 수신용 고정 버퍼
        /// </summary>
        private byte[] fixedReadBuffer;

        /// <summary>
        /// 송신용 고정 버퍼
        /// </summary>
        private byte[] fixedWriteBuffer;

        /// <summary>
        /// 클라이언트와 연결된 네트워크 스트림
        /// </summary>
        private NetworkStream? stream;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="lengthSize">패킷 길이 필드의 크기 (바이트)</param>
        /// <param name="typeSize">패킷 타입 필드의 크기 (바이트)</param>
        /// <param name="maxLength">허용할 최대 패킷 크기 (바이트)</param>
        public PacketParser(int lengthSize = Packet.PACKET_HEADER_LEN_SIZE, int typeSize = Packet.PACKET_HEADER_TYPE_SIZE, int maxLength = Packet.PACKET_MAX_SIZE)
        {
            this.lengthSize = lengthSize;
            this.typeSize = typeSize;
            this.maxLength = maxLength;

            // 헤더 읽기용 버퍼 별도 할당
            this.headerReadBuffer = new byte[lengthSize + typeSize];
            // 읽기, 쓰기용 고정 버퍼 할당
            this.fixedReadBuffer = new byte[FIXED_BUFFER_SIZE];
            this.fixedWriteBuffer = new byte[FIXED_BUFFER_SIZE];

            this.stream = null;
        }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="stream">연결된 네트워크 스트림</param>
        /// <param name="lengthSize">패킷 길이 필드의 크기</param>
        /// <param name="typeSize">패킷 타입 필드의 크기</param>
        /// <param name="maxLength">최대 패킷 크기</param>
        public PacketParser(NetworkStream stream, int lengthSize = Packet.PACKET_HEADER_LEN_SIZE, int typeSize = Packet.PACKET_HEADER_TYPE_SIZE, int maxLength = Packet.PACKET_MAX_SIZE)
            : this(lengthSize, typeSize, maxLength)
        {
            this.stream = stream;
        }

        /// <summary>
        /// 네트워크 스트림을 설정
        /// </summary>
        public void SetStream(NetworkStream stream) => this.stream = stream;

        /// <summary>
        /// 네트워크 스트림 설정을 초기화
        /// </summary>
        public void ResetStream() => this.stream = null;

        /// <summary>
        /// 현재 설정된 네트워크 스트림 반환
        /// </summary>
        public NetworkStream? GetStream() => this.stream;

        /// <summary>
        /// 헤더의 길이 필드 크기를 반환
        /// </summary>
        public int GetLengthSize() => lengthSize;

        /// <summary>
        /// 헤더의 타입 필드 크기를 반환
        /// </summary>
        public int GetTypeSize() => typeSize;

        /// <summary>
        /// 헤더 전체 크기를 반환 (길이 필드 + 타입 필드)
        /// </summary>
        public int GetHeaderSize() => GetTypeSize() + GetLengthSize();

        /// <summary>
        /// 패킷의 최대 크기를 반환
        /// </summary>
        public int GetMaxLength() => maxLength;

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

            // 1. 헤더 읽기 (전용 헤더 버퍼 사용)
            try 
            {
                await stream.ReadExactlyAsync(headerReadBuffer.AsMemory(0, headerSize)).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                throw new ObjectDisposedException("network stream");
            }

            // 2. 헤더 파싱 (메모리 할당 없이 변환)
            ReadOnlySpan<byte> headerSpan = headerReadBuffer.AsSpan(0, headerSize);
            int packetLength = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(0, this.lengthSize));
            int packetType = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(this.lengthSize, this.typeSize));

            // 패킷 크기 유효성 검사
            if (packetLength > GetMaxLength() || packetLength < headerSize)
            {
                throw new ArgumentOutOfRangeException($"Invalid Packet size: {packetLength}");
            }

            // 3. 바디 데이터 읽기
            int bodySize = packetLength - headerSize;
            ReadOnlyMemory<byte> bodyMemory = ReadOnlyMemory<byte>.Empty;

            if (bodySize > 0)
            {
                byte[]? rentedBuffer = null;
                Memory<byte> targetBuffer;

                // 바디 크기가 8KB를 초과하면 ArrayPool 사용
                if (bodySize > FIXED_BUFFER_SIZE)
                {
                    rentedBuffer = ArrayPool<byte>.Shared.Rent(bodySize);
                    targetBuffer = rentedBuffer.AsMemory(0, bodySize);
                }
                else
                {
                    // 8KB 이하일 경우 고정 버퍼 재사용
                    targetBuffer = fixedReadBuffer.AsMemory(0, bodySize);
                }

                try
                {
                    // 바디 데이터 수신
                    await stream.ReadExactlyAsync(targetBuffer).ConfigureAwait(false);
                    
                    if (rentedBuffer == null)
                    {
                        // 고정 버퍼 사용 시, 메모리 복사 없이 전달
                        bodyMemory = targetBuffer;
                    }
                    else
                    {
                        // ArrayPool 사용 시, 버퍼 반납이 필요하니 일단 복사하지만, 대용량 패킷은 적으니 일단 감수
                        bodyMemory = targetBuffer.ToArray();
                        ArrayPool<byte>.Shared.Return(rentedBuffer);    // 반납
                    }
                }
                catch (EndOfStreamException)
                {
                    if (rentedBuffer != null) ArrayPool<byte>.Shared.Return(rentedBuffer);
                    throw new ObjectDisposedException("network stream");
                }
            }

            // 패킷 객체 생성 및 반환
            // BodyMemory(byte[])를 통해 데이터를 전달
            return new MyPacket
            {
                Len = packetLength,
                Type = packetType,
                Body = null, 
                BodyMemory = bodyMemory
            };
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
                bool useBodyMemory = false;

                // BodyMemory에 데이터가 있으면 우선 사용하고, 없으면 Body(문자열)를 사용
                if (!packet.BodyMemory.IsEmpty && string.IsNullOrEmpty(packet.Body))
                {
                    bodyByteCount = packet.BodyMemory.Length;
                    useBodyMemory = true;
                }
                else if (!string.IsNullOrEmpty(packet.Body))
                {
                    bodyByteCount = Encoding.UTF8.GetByteCount(packet.Body);
                }

                int totalSize = GetHeaderSize() + bodyByteCount;
                
                byte[]? rentedBuffer = null;
                Memory<byte> bufferMemory;
                Span<byte> bufferSpan;

                // 전송할 데이터가 8KB를 초과하면 ArrayPool 사용
                if (totalSize > FIXED_BUFFER_SIZE)
                {
                    rentedBuffer = ArrayPool<byte>.Shared.Rent(totalSize);
                    bufferMemory = rentedBuffer.AsMemory(0, totalSize);
                    bufferSpan = rentedBuffer.AsSpan(0, totalSize);
                }
                else
                {
                    // 8KB 이하일 경우 고정 버퍼 사용
                    bufferMemory = fixedWriteBuffer.AsMemory(0, totalSize);
                    bufferSpan = fixedWriteBuffer.AsSpan(0, totalSize);
                }

                try
                {
                    packet.Len = totalSize;

                    // 1. 헤더 쓰기
                    BinaryPrimitives.WriteInt32LittleEndian(bufferSpan.Slice(0, this.lengthSize), packet.Len);
                    BinaryPrimitives.WriteInt32LittleEndian(bufferSpan.Slice(this.lengthSize, this.typeSize), packet.Type);

                    // 2. 바디 쓰기
                    if (bodyByteCount > 0)
                    {
                        if (useBodyMemory)
                        {
                            // 메모리에서 직접 복사
                            packet.BodyMemory.Span.CopyTo(bufferSpan.Slice(GetHeaderSize(), bodyByteCount));
                        }
                        else if (packet.Body != null)
                        {
                            // 문자열을 UTF8 바이트로 인코딩하여 버퍼에 기록
                            Encoding.UTF8.GetBytes(packet.Body, bufferSpan.Slice(GetHeaderSize(), bodyByteCount));
                        }
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
        /// 리소스 해제
        /// </summary>
        public void Dispose()
        {
        }
    }
}
