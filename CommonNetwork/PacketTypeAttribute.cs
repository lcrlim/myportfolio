using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 숫자 타입과 C# 패킷 클래스를 연결해주는 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PacketTypeAttribute : Attribute
    {
        public int Type { get; }

        public PacketTypeAttribute(int type) => Type = type;
    }
}
