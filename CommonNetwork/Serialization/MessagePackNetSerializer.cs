using System;
using System.Buffers;
using MessagePack;

namespace MyCommonNet.Serialization
{
    /// <summary>
    /// MessagePack 기반 직렬화 구현
    /// 고성능 바이너리 직렬화 (JSON보다 작고 빠름)
    /// </summary>
    public sealed class MessagePackNetSerializer : ISerializer
    {
        private static readonly MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.None); // 압축 비활성화로 속도 우선

        /// <summary>
        /// 직렬화 포맷 이름
        /// </summary>
        public string FormatName => "MessagePack";

        /// <summary>
        /// 객체를 Span 버퍼에 직렬화
        /// </summary>
        /// <typeparam name="T">직렬화할 타입</typeparam>
        /// <param name="value">직렬화할 객체</param>
        /// <param name="buffer">출력 버퍼</param>
        /// <returns>실제 쓰여진 바이트 수</returns>
        public int Serialize<T>(T value, Span<byte> buffer)
        {
            using var writer = new PooledBufferWriter(buffer.Length);
            MessagePackSerializer.Serialize(writer, value, _options);

            var written = writer.WrittenSpan;
            if (written.Length > buffer.Length)
            {
                throw new ArgumentException($"버퍼 크기가 부족합니다. 필요: {written.Length}, 제공: {buffer.Length}");
            }

            written.CopyTo(buffer);
            return written.Length;
        }

        /// <summary>
        /// 직렬화에 필요한 버퍼 크기 추정
        /// MessagePack은 JSON보다 작으므로 더 작은 값 반환
        /// </summary>
        public int GetMaxSize<T>(T value)
        {
            // MessagePack은 바이너리이므로 JSON보다 작음
            return 2048;
        }

        /// <summary>
        /// IBufferWriter를 사용한 직렬화 (Zero-allocation)
        /// </summary>
        public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
        {
            MessagePackSerializer.Serialize(bufferWriter, value, _options);
        }

        /// <summary>
        /// ReadOnlySpan 기반 역직렬화
        /// </summary>
        public T? Deserialize<T>(ReadOnlySpan<byte> data)
        {
            // MessagePack은 ReadOnlyMemory를 사용
            return MessagePackSerializer.Deserialize<T>(new ReadOnlySequence<byte>(data.ToArray()), _options);
        }

        /// <summary>
        /// ReadOnlyMemory 기반 역직렬화 (더 효율적)
        /// </summary>
        public T? Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            return MessagePackSerializer.Deserialize<T>(data, _options);
        }

        /// <summary>
        /// 동적 타입 역직렬화
        /// </summary>
        public object? Deserialize(ReadOnlySpan<byte> data, Type targetType)
        {
            return MessagePackSerializer.Deserialize(targetType, new ReadOnlySequence<byte>(data.ToArray()), _options);
        }
    }
}
