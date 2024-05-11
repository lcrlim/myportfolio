using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommonNetwork
{
    /// <summary>
    /// 네트워크에서 수신한 패킷을 파싱하고, 객체 패킷을 응답으로 전송하기 위한 parser
    /// </summary>
    public class PacketParser
    {
        /// <summary>
        /// header 길이 버퍼
        /// </summary>
        byte[] lengthBuffer = new byte[Packet.PACKET_HEADER_LEN_SIZE];
        /// <summary>
        /// 헤더 타입 버퍼
        /// </summary>
        byte[] typeBuffer = new byte[Packet.PACKET_HEADER_TYPE_SIZE];
        // 커넥션 스트림
        NetworkStream? stream;

        public PacketParser()
        {
        }

        public PacketParser(NetworkStream stream)
        {
            this.stream = stream;
        }

        public void SetStream(NetworkStream stream)
        {
            this.stream = stream;
        }

        public async Task<PacketBase> ReadPacketBase()
        {
            if (stream == null)
            {
                throw new Exception("Network stream is null");
            }

            // 버퍼 초기화
            Array.Clear(lengthBuffer, 0, lengthBuffer.Length);
            Array.Clear(typeBuffer, 0, typeBuffer.Length);

            // 패킷의 길이 읽기
            int readSize = await this.stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length).ConfigureAwait(false);
            if (readSize == 0)
            {
                throw new ObjectDisposedException(stream.ToString());
            }

            int packetLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (packetLength > Packet.PACKET_MAX_SIZE)
            {
                throw new ArgumentOutOfRangeException("Packet size too big");
            }

            // 패킷의 타입 읽기
            readSize = await stream.ReadAsync(typeBuffer, 0, typeBuffer.Length).ConfigureAwait(false);
            if (readSize == 0)
            {
                throw new ObjectDisposedException(stream.ToString());
            }
            int packetType = BitConverter.ToInt32(typeBuffer, 0);

            // 데이터 읽기
            byte[] dataBuffer = new byte[packetLength - (lengthBuffer.Length + typeBuffer.Length)];
            readSize = await stream.ReadAsync(dataBuffer, 0, packetLength - (lengthBuffer.Length + typeBuffer.Length)).ConfigureAwait(false); // 데이터 길이에서 헤더 길이(8바이트)를 뺌
            if (readSize == 0)
            {
                throw new ObjectDisposedException(stream.ToString());
            }

            // 수신된 데이터를 문자열로 변환
            string dataReceived = Encoding.UTF8.GetString(dataBuffer, 0, readSize);

            return new PacketBase
            {
                Len = packetLength,
                Type = packetType,
                Body = dataReceived
            };
        }

        public async Task WritePacketBase(PacketBase packet)
        {
            // 데이터 string이 있으면 byte로 변환
            byte[]? resData = null;
            if (packet.Body != null)
                resData = Encoding.UTF8.GetBytes(packet.Body);

            byte[] resBytes = new byte[Packet.PACKET_HEADER_LEN_SIZE + Packet.PACKET_HEADER_TYPE_SIZE + (resData?.Length ?? 0)];

            // length 바이트로
            BitConverter.GetBytes(Packet.PACKET_HEADER_LEN_SIZE + Packet.PACKET_HEADER_TYPE_SIZE + (resData?.Length ?? 0)).CopyTo(resBytes, 0);

            // type 바이트로
            BitConverter.GetBytes(packet.Type).CopyTo(resBytes, Packet.PACKET_HEADER_LEN_SIZE);

            if (resData != null)
            {
                Array.Copy(resData, 0, resBytes, Packet.PACKET_HEADER_SIZE, resData.Length);
            }

            await stream.WriteAsync(resBytes).ConfigureAwait(false);
        }
    }
}
