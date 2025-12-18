using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using static MyCommonNet.Packet;

namespace MyCommonNet
{
    /// <summary>
    /// PING 요청 패킷
    /// </summary>
    [MessagePackObject]
    [PacketType((int)PacketType.PING)]
    public sealed class PacketPing : MyPacket
    {
        [Key(0)]
        public int Num { get; set; }

        [Key(1)]
        public string Str { get; set; } = string.Empty;
    }

    /// <summary>
    /// PONG 응답 패킷
    /// </summary>
    [MessagePackObject]
    [PacketType((int)PacketType.PONG)]
    public sealed class PacketPong : MyPacket
    {
        [Key(0)]
        public int Num { get; set; }

        [Key(1)]
        public string Str { get; set; } = string.Empty;
    }

    /// <summary>
    /// 로그인 요청 패킷
    /// </summary>
    [MessagePackObject]
    [PacketType((int)PacketType.LOGIN)]
    public sealed class PacketLogin : MyPacket
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 로그인 응답 패킷
    /// </summary>
    [MessagePackObject]
    [PacketType((int)PacketType.LOGIN_RESULT)]
    public sealed class PacketLoginResult : MyPacket
    {
        [Key(0)]
        public bool Success { get; set; }
    }

    /// <summary>
    /// 캐릭터 목록 요청 패킷
    /// </summary>
    [MessagePackObject]
    [PacketType((int)PacketType.CHARACTER_LIST)]
    public sealed class PacketCharacterList : MyPacket
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 캐릭터 목록 응답 패킷
    /// </summary>
    [MessagePackObject]
    [PacketType((int)PacketType.CHARACTER_LIST_RESULT)]
    public sealed class PacketCharacterListResult : MyPacket
    {
        [Key(0)]
        public List<Character> Characters { get; set; } = new List<Character>();
    }

    /// <summary>
    /// 캐릭터 정보
    /// </summary>
    [MessagePackObject]
    public class Character
    {
        [Key(0)]
        public long Id { get; set; }

        [Key(1)]
        public string Name { get; set; } = string.Empty;

        [Key(2)]
        public int Class { get; set; }

        [Key(3)]
        public int Level { get; set; }
    }
}
