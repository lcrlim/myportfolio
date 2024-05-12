using Newtonsoft.Json;
using Nito.Async;
using Nito.Async.Sockets;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerFramework
{
    public enum SocketState
    {
        Closed = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
    }
    public class MyClientSocket : IDisposable
    {
        ActionThread thread;
        IPAddress ip;
        int port;
        ManualResetEventSlim ev;
        object reLock = new object();
        SimpleClientTcpSocket socket;

        public SocketState State { get; set; }
        
        public MyClientSocket(string ip, int port, bool autoReconnect = true)
        {
            if (!IPAddress.TryParse(ip, out this.ip))
            {
                return;
            }
            this.port = port;
            thread = new ActionThread();
            thread.Start();

            Init();
        }

        void Reset()
        {
            lock (reLock)
            {
                if (socket != null)
                {
                    socket.ConnectCompleted -= SocketConnectCompleted;
                    socket.PacketArrived -= SocketPacketArrived;
                    socket.WriteCompleted -= (args) => SocketWriteCompleted(socket, args);
                    socket.ShutdownCompleted -= SocketShutdownCompleted;
                    socket.Close();
                    socket = null;
                    ev?.Dispose();
                    ev = null;
                }
                State = SocketState.Closed;
            }
        }

        public void Reconnect(bool forced = false)
        {
            lock(reLock)
            {
                if (forced || State == SocketState.Closed)
                {
                    Reset();
                    Init();
                }
            }
        }

        private void SocketConnectCompleted(AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    Reset();
                    Log.Logger.Warning($"Socket connect error - {e.Error.Message}");
                    return;
                }

                State = SocketState.Connected;
                Log.Logger.Information($"Socket connected - {this.socket?.RemoteEndPoint?.ToString()}");
            }
            catch (Exception ex)
            {
                Reset();
                Log.Logger.Error($"Socket connect error - {ex.ToString()}");
            }
        }

        private void SocketWriteCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Reset();
                Log.Logger.Error($"Socket write error - {e.Error.Message}");
            }
        }

        private void SocketShutdownCompleted(AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Reset();
                Log.Logger.Error($"Socket shutdown error - {e.Error.Message}");
            }
            else
            {
                Reset();
                Log.Logger.Information($"Socket shutdown complete");
            }
        }

        private void SocketPacketArrived(AsyncResultEventArgs<byte[]> e)
        {
            try
            {
                if (e.Error != null)
                {
                    Reset();
                    Log.Logger.Warning($"Socket read error - {e.Error.Message}");
                }
                else if (e.Result == null)
                {
                    Log.Logger.Warning($"Socket graceful close");
                    Reset();
                }
                else
                {
                    Packet packet = SerializeExtention.Deserialize<Packet>(e.Result);
                    PacketEventHandler.Instance.Publish(packet);
                }
            }
            catch (Exception ex)
            {
                Reset();
                Log.Logger.Error($"Socket read error - {ex.ToString()}");
            }
        }

        private void Init()
        {
            thread.DoSynchronously(() =>
            {
                try
                {
                    socket = new SimpleClientTcpSocket();
                    socket.ConnectCompleted += SocketConnectCompleted;
                    socket.PacketArrived += SocketPacketArrived;
                    socket.WriteCompleted += (args) => SocketWriteCompleted(socket, args);
                    socket.ShutdownCompleted += SocketShutdownCompleted;

                    socket.ConnectAsync(this.ip, this.port);
                    State = SocketState.Connecting;
                    Log.Logger.Information($"Connecting {new IPEndPoint(ip, port).ToString()}");
                }
                catch (Exception ex)
                {
                    Reset();
                    Log.Logger.Error($"Socket connect error - {ex.ToString()}");
                }
            });
        }

        public void Disconnect()
        {
            try
            {
                socket.ShutdownAsync();
                State = SocketState.Disconnecting;
                Log.Logger.Information("Socket disconnecting");
            }
            catch (Exception ex)
            {
                Reset();
                Log.Logger.Error($"Socket disconnet error - {ex.ToString()}");
            }
        }

        public void AbortiveClose()
        {
            try
            {
                socket.AbortiveClose();
                socket = null;
                State = SocketState.Closed;
            }
            catch (Exception ex)
            {
                Reset();
                Log.Logger.Error($"Socket abortive close error - {ex.ToString()}");
            }
        }

        public Task<Packet> RequestAsync(Packet req, int? timeoutSec = null)
        {
            TaskCompletionSource<Packet> tcs = new TaskCompletionSource<Packet>();

            var timer = new System.Threading.Timer(s =>
            {
                ((System.Threading.Timer)s).Dispose();
                tcs.TrySetException(new TimeoutException($"Request timed out"));
                PacketEventHandler.Instance.RemoveExpirePacket();
            });
            int tcpResponseTimeout = 10 * 1000;
            if (timeoutSec != null)
            {
                tcpResponseTimeout = timeoutSec.Value * 1000;
            }

            timer.Change(TimeSpan.FromMilliseconds(tcpResponseTimeout));

            try
            {
                PacketEventHandler.Instance.StoreContext(new PacketEventHandler.PacketEventArgs
                {
                    Packet = req,
                    TaskSorce = tcs,
                    Timer = timer,
                    RequestDate = DateTime.Now,
                });

                byte[] bytes = SerializeExtension.Serialize(req);
                if (socket == null)
                    throw new Exception("Disposed socket");

                if (State == SocketState.Connecting)
                {
                    int wait = 30;
                    if (wait > 0)
                        Thread.Sleep(wait);
                }

                if (State == SocketState.Connecting)
                {
                    int timeout = 10;
                    if (ev != null)
                    {
                        ev.Wait(timeout);
                    }

                    if (State != SocketState.Connected)
                    {
                        throw new Exception("Disconnected socket");
                    }
                }
                else if (State != SocketState.Connected)
                {
                    throw new Exception("Disconnected socket");
                }

                socket.WriteAsync(bytes);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                Reset();
                Log.Logger.Warning($"Socket send error - {ex.ToString()}");
            }

            return tcs.Task;
        }

        public void Dispose()
        {
            Reset();
            thread?.Dispose();
            thread = null;
            ev?.Dispose();
            ev = null;
        }
    }
}
