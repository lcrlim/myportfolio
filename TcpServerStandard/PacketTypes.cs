using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerStandard
{
    public class Packet
    {
        public const int PACKET_HEADER_LEN_SIZE = 4;
        public const int PACKET_HEADER_TYPE_SIZE = 4;
        public const int PACKET_HEADER_SIZE = PACKET_HEADER_LEN_SIZE + PACKET_HEADER_TYPE_SIZE;
        public const int PACKET_MAX_SIZE = 65536;

        public enum Type
        {
            PING = 1,
            PONG = 2,
        }
    }
}
