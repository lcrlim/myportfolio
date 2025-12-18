using System;
using System.Buffers;

namespace MyCommonNet.Serialization
{
    /// <summary>
    /// 직렬화를 위한 추상화 인터페이스
    /// JSON, Protobuf, MessagePack 등 다양한 직렬화 방식 지원
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// 직렬화 포맷 식별자 (예: "JSON", "MessagePack", "Protobuf")
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// 객체를 버퍼에 직렬화
        /// </summary>
        /// <typeparam name="T">직렬화할 타입</typeparam>
        /// <param name="value">직렬화할 객체</param>
        /// <param name="buffer">출력 버퍼</param>
        /// <returns>실제 쓰여진 바이트 수</returns>
        int Serialize<T>(T value, Span<byte> buffer);

        /// <summary>
        /// 직렬화에 필요한 버퍼 크기 추정
        /// </summary>
        /// <typeparam name="T">직렬화할 타입</typeparam>
        /// <param name="value">직렬화할 객체</param>
        /// <returns>추정 버퍼 크기 (바이트)</returns>
        int GetMaxSize<T>(T value);

        /// <summary>
        /// IBufferWriter를 사용한 직렬화
        /// </summary>
        /// <typeparam name="T">직렬화할 타입</typeparam>
        /// <param name="value">직렬화할 객체</param>
        /// <param name="bufferWriter">출력 버퍼 라이터</param>
        void Serialize<T>(T value, IBufferWriter<byte> bufferWriter);

        /// <summary>
        /// ReadOnlySpan 기반 역직렬화
        /// </summary>
        /// <typeparam name="T">역직렬화 대상 타입</typeparam>
        /// <param name="data">입력 데이터</param>
        /// <returns>역직렬화된 객체</returns>
        T? Deserialize<T>(ReadOnlySpan<byte> data);

        /// <summary>
        /// ReadOnlyMemory 기반 역직렬화
        /// </summary>
        /// <typeparam name="T">역직렬화 대상 타입</typeparam>
        /// <param name="data">입력 데이터</param>
        /// <returns>역직렬화된 객체</returns>
        T? Deserialize<T>(ReadOnlyMemory<byte> data);

        /// <summary>
        /// 동적 타입 역직렬화 (런타임 타입 지정)
        /// </summary>
        /// <param name="data">입력 데이터</param>
        /// <param name="targetType">역직렬화 대상 타입</param>
        /// <returns>역직렬화된 객체</returns>
        object? Deserialize(ReadOnlySpan<byte> data, Type targetType);
    }
}
