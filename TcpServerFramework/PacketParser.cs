using Nito.Async;
using Nito.Async.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerFramework
{
    public class PacketParser
    {
        byte[] lengthBuffer;
        byte[] dataBuffer;
        int bytesReceived;

        public PacketParser(IAsyncTcpConnection socket)
        {
            this.Socket = socket;
            int len = 4;
            this.lengthBuffer = new byte[len];
        }

        public event Action<AsyncResultEventArgs<byte[]>> PacketArrived;

        public IAsyncTcpConnection Socket {  get; private set; }

        public static void WritePacketAsync(IAsyncTcpConnection socket, byte[] packet, object state)
        {
            socket.WriteAsync(packet, state);
        }

        public static void WritePacketAsync(IAsyncTcpConnection socket, byte[] packet)
        {
            WritePacketAsync(socket, packet, null);
        }

        public void Start()
        {
            this.Socket.ReadCompleted += this.SocketReadCompleted;
            this.ContinueReading();
        }

        private void ContinueReading()
        {
            if (this.dataBuffer != null)
            {
                this.Socket.ReadAsync(this.dataBuffer, this.bytesReceived, this.dataBuffer.Length - this.bytesReceived);
            }
            else
            {
                this.Socket.ReadAsync(this.lengthBuffer, this.bytesReceived, this.lengthBuffer.Length - this.bytesReceived);
            }
        }

        private void SocketReadCompleted(AsyncResultEventArgs<int> e)
        {
            // read error
            if (e.Error != null)
            {
                this.PacketArrived?.Invoke(new AsyncResultEventArgs<byte[]>(e.Error));
                return;
            }

            this.bytesReceived += e.Result;
            // zero length is closed
            if (e.Result == 0)
            {
                this.PacketArrived?.Invoke(new AsyncResultEventArgs<byte[]>((byte[])null));
                return;
            }

            if (this.dataBuffer == null)
            {
                if (this.bytesReceived != lengthBuffer.Length)
                {
                    this.ContinueReading();
                }
                else
                {
                    int length = 0;
                    if (lengthBuffer.Length == 16)
                    {
                        // 패킷 구조가 id(4 byte), seq(8byte), length(4byte) 이기때문에 12 바이트 이후 값을 length로 가져온다.
                        length = BitConverter.ToInt32(lengthBuffer, 12);
                    }

                    if (length < 0)
                    {
                        this.PacketArrived?.Invoke(new AsyncResultEventArgs<byte[]>(new System.IO.InvalidDataException("Packet length less than zero (corrupted message)")));
                        return;
                    }
                    else if (length == 0)
                    {
                        // databuffer가 없으니 length만 전달
                        this.PacketArrived?.Invoke(new AsyncResultEventArgs<byte[]>(this.lengthBuffer));
                        this.dataBuffer = null;
                        this.bytesReceived = 0;
                        this.ContinueReading();
                    }
                    else
                    {
                        this.dataBuffer = new byte[length];
                        this.bytesReceived = 0;
                        this.ContinueReading();
                    }
                }
            }
            else
            {
                if (this.bytesReceived != this.dataBuffer.Length)
                {
                    this.ContinueReading();
                }
                else
                {
                    var combined = new byte[this.lengthBuffer.Length + this.dataBuffer.Length];
                    this.lengthBuffer.CopyTo(combined, 0);
                    this.dataBuffer.CopyTo(combined, this.lengthBuffer.Length);

                    this.PacketArrived?.Invoke(new AsyncResultEventArgs<byte[]>(combined));

                    this.dataBuffer = null;
                    this.bytesReceived = 0;
                    this.ContinueReading();
                }
            }
        }
    }
}
