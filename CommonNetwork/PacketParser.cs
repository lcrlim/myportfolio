using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 네트워크에서 수신한 패킷을 파싱하고, 객체 패킷을 응답으로 전송하기 위한 parser
    /// 별도 parser를 구현할 경우 이 클래스를 상속받아 virtual 메소드를 override 하면 된다.
    /// </summary>
    public class PacketParser : IPacketParser
    {
        int lengthSize = 0;
        int typeSize = 0;
        int maxLength = 0;
        /// <summary>
        /// header 길이 버퍼
        /// </summary>
        byte[] lengthBuffer;
        /// <summary>
        /// 헤더 타입 버퍼
        /// </summary>
        byte[] typeBuffer;
        // 클라이언트의 네트워크 스트림
        NetworkStream? stream;

        /// <summary>
        /// 클라이언트 스트림 없이 파서 생성
        /// </summary>
        /// <param name="lengthSize"></param>
        /// <param name="typeSize"></param>
        /// <param name="maxLength"></param>
        public PacketParser(int lengthSize = Packet.PACKET_HEADER_LEN_SIZE, int typeSize = Packet.PACKET_HEADER_TYPE_SIZE, int maxLength = Packet.PACKET_MAX_SIZE)
        {
            this.lengthSize = lengthSize;
            this.typeSize = typeSize;
            this.maxLength = maxLength;

            lengthBuffer = new byte[lengthSize];
            typeBuffer = new byte[typeSize];
        }

        /// <summary>
        /// 클라이언트의 스트림 저장하면서 파서 생성
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="lengthSize"></param>
        /// <param name="typeSize"></param>
        /// <param name="maxLength"></param>
        public PacketParser(NetworkStream stream, int lengthSize = Packet.PACKET_HEADER_LEN_SIZE, int typeSize = Packet.PACKET_HEADER_TYPE_SIZE, int maxLength = Packet.PACKET_MAX_SIZE)
        {
            this.stream = stream;
            this.lengthSize = lengthSize;
            this.typeSize = typeSize;
            this.maxLength = maxLength;

            lengthBuffer = new byte[lengthSize];
            typeBuffer = new byte[typeSize];
        }

        /// <summary>
        /// 파서 생성 이후 클라이언트 네트워크 스트림을 설정
        /// 스트림이 설정되지 않으면 정상 동작하지 않는다.
        /// </summary>
        /// <param name="stream"></param>
        public void SetStream(NetworkStream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        /// 네트워크 스트림 조회
        /// </summary>
        /// <returns></returns>
        public NetworkStream? GetStream()
        {
            return this.stream;
        }

        public int GetLengthSize()
        {
            return lengthSize;
        }

        public int GetTypeSize()
        {
            return typeSize;
        }

        public int GetHeaderSize()
        {
            return GetTypeSize() + GetLengthSize();
        }

        public int GetMaxLength()
        {
            return maxLength;
        }

        /// <summary>
        /// 패킷 읽기 메소드, 상속 클래스 구현시 override 대상
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception">일반 오류</exception>
        /// <exception cref="ObjectDisposedException">커넥션 끊김</exception>
        /// <exception cref="ArgumentOutOfRangeException">최대 패킷 길이 초과</exception>
        public virtual async Task<MyPacket> ReadPacket()
        {
            if (GetStream() == null)
            {
                throw new Exception("Network stream is null");
            }

            // 버퍼 초기화
            Array.Clear(lengthBuffer, 0, lengthBuffer.Length);
            Array.Clear(typeBuffer, 0, typeBuffer.Length);

            // 패킷의 길이 읽기
            int readSize = await GetStream().ReadAsync(lengthBuffer, 0, lengthBuffer.Length)
                .ConfigureAwait(false);
            if (readSize == 0)
            {
                throw new ObjectDisposedException("network stream");
            }

            int packetLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (packetLength > GetMaxLength())
            {
                throw new ArgumentOutOfRangeException("Packet size too big");
            }

            // 패킷의 타입 읽기
            readSize = await GetStream().ReadAsync(typeBuffer, 0, typeBuffer.Length)
                .ConfigureAwait(false);
            if (readSize == 0)
            {
                throw new ObjectDisposedException("network stream");
            }
            int packetType = BitConverter.ToInt32(typeBuffer, 0);

            // 데이터 읽기
            byte[] dataBuffer = new byte[packetLength - (lengthBuffer.Length + typeBuffer.Length)];
            // 데이터 길이에서 헤더 길이를 뺌
            readSize = await GetStream().ReadAsync(dataBuffer, 0, packetLength - (lengthBuffer.Length + typeBuffer.Length))
                .ConfigureAwait(false);
            if (readSize == 0)
            {
                throw new ObjectDisposedException("network stream");
            }

            // 수신된 데이터를 문자열로 변환
            string dataReceived = Encoding.UTF8.GetString(dataBuffer, 0, readSize);

            return new MyPacket
            {
                Len = packetLength,
                Type = packetType,
                Body = dataReceived
            };
        }

        /// <summary>
        /// 패킷 쓰기 메소드, 상속 클래스 구현시 override 대상
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public virtual async Task WritePacket(MyPacket packet)
        {
            // 데이터 string이 있으면 byte로 변환
            byte[]? resData = null;
            if (packet.Body != null)
                resData = Encoding.UTF8.GetBytes(packet.Body);

            byte[] resBytes = new byte[GetHeaderSize() + (resData?.Length ?? 0)];

            // length 바이트로
            // 패킷 길이는 헤더사이즈 + 바디 사이즈로 설정한다.
            packet.Len = GetHeaderSize() + (resData == null ? 0 : resData.Length);
            BitConverter.GetBytes(packet.Len).CopyTo(resBytes, 0);

            // type 바이트로
            BitConverter.GetBytes(packet.Type).CopyTo(resBytes, GetLengthSize());

            if (resData != null)
            {
                Array.Copy(resData, 0, resBytes, GetHeaderSize(), resData.Length);
            }

            await GetStream().WriteAsync(resBytes).ConfigureAwait(false);
        }
    }
}
