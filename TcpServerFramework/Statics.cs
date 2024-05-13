using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerFramework
{
    public class Statics
    {
        public const int PacketTypeSize = 4;
        public const int PacketSeqSize = 8;
        public const int PacketLengthSize = 4;
        public const int PacketHeaderSize = 16;
    }
}
