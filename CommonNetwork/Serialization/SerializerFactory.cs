using System;

namespace MyCommonNet.Serialization
{
    /// <summary>
    /// 직렬화 방식 열거형
    /// </summary>
    public enum SerializerType
    {
        /// <summary>
        /// System.Text.Json 기반 (기본값, 호환성 우선)
        /// </summary>
        Json,

        /// <summary>
        /// MessagePack 기반
        /// </summary>
        MessagePack,

        /// <summary>
        /// Protobuf 기반
        /// </summary>
        Protobuf
    }

    /// <summary>
    /// 직렬화기 팩토리
    /// 설정에 따라 적절한 ISerializer 구현체 생성
    /// </summary>
    public static class SerializerFactory
    {
        /// <summary>
        /// 지정된 타입의 직렬화기 생성
        /// </summary>
        /// <param name="type">직렬화 방식</param>
        /// <returns>ISerializer 구현체</returns>
        /// <exception cref="ArgumentOutOfRangeException">지원하지 않는 직렬화 타입</exception>
        public static ISerializer Create(SerializerType type)
        {
            return type switch
            {
                SerializerType.Json => new JsonNetSerializer(),
                SerializerType.MessagePack => new MessagePackNetSerializer(),
                SerializerType.Protobuf => throw new NotImplementedException("Protobuf 직렬화기는 아직 구현되지 않았습니다. protobuf-net 패키지 추가 후 구현 필요."),
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"지원하지 않는 직렬화 타입입니다: {type}")
            };
        }

        /// <summary>
        /// 기본 직렬화기 생성 (JSON)
        /// </summary>
        /// <returns>JSON 직렬화기</returns>
        public static ISerializer CreateDefault()
        {
            return new JsonNetSerializer();
        }
    }
}
