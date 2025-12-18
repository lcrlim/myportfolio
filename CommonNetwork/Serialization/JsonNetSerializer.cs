using System;
using System.Buffers;
using System.Text.Json;

namespace MyCommonNet.Serialization
{
    /// <summary>
    /// System.Text.Json 기반 직렬화 구현
    /// 기존 호환성 유지하면서 Zero-allocation 최적화
    /// </summary>
    public sealed class JsonNetSerializer : ISerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = null, // PascalCase 유지
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// 직렬화 포맷 이름
        /// </summary>
        public string FormatName => "JSON";

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
            using var jsonWriter = new Utf8JsonWriter(writer);

            // 런타임 타입으로 직렬화 (파생 클래스의 속성도 포함)
            JsonSerializer.Serialize(jsonWriter, value, value!.GetType(), _options);
            jsonWriter.Flush();

            var written = writer.WrittenSpan;
            if (written.Length > buffer.Length)
            {
                throw new ArgumentException($"not enough buffer, require: {written.Length}, current: {buffer.Length}");
            }

            written.CopyTo(buffer);
            return written.Length;
        }

        /// <summary>
        /// 직렬화에 필요한 버퍼 크기 추정
        /// JSON은 정확한 크기 예측이 어려우므로 넉넉하게 반환
        /// </summary>
        public int GetMaxSize<T>(T value)
        {
            // JSON은 텍스트 기반이므로 바이너리보다 크기가 큼
            // 대략적인 추정값 반환
            return 4096;
        }

        /// <summary>
        /// IBufferWriter를 사용한 직렬화
        /// </summary>
        public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
        {
            using var jsonWriter = new Utf8JsonWriter(bufferWriter);
            // 런타임 타입으로 직렬화 (파생 클래스의 속성도 포함)
            JsonSerializer.Serialize(jsonWriter, value, value!.GetType(), _options);
            jsonWriter.Flush();
        }

        /// <summary>
        /// ReadOnlySpan 기반 역직렬화
        /// </summary>
        public T? Deserialize<T>(ReadOnlySpan<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data, _options);
        }

        /// <summary>
        /// ReadOnlyMemory 기반 역직렬화
        /// </summary>
        public T? Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            return JsonSerializer.Deserialize<T>(data.Span, _options);
        }

        /// <summary>
        /// 동적 타입 역직렬화
        /// </summary>
        public object? Deserialize(ReadOnlySpan<byte> data, Type targetType)
        {
            return JsonSerializer.Deserialize(data, targetType, _options);
        }
    }
}
