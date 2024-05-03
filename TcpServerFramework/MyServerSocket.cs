using Nito.Async;
using Nito.Async.Sockets;
using Serilog;
using System;
using System.ComponentModel;
using System.Threading;

namespace TcpServerFramework
{
    public class MyServerSocket : IDisposable
    {
        private ActionThread actionThread;

        private SimpleServerTcpSocket serverSocket;

        private ManualResetEventSlim einit = new ManualResetEventSlim(false);

        public int Port { get; set; }

        public Action<SimpleServerChildTcpSocket, byte[]> RecvAction;

        public Action<SimpleServerChildTcpSocket> AcceptAction;

        public event Action<AsyncResultEventArgs<SimpleServerChildTcpSocket>> ConnectAction;

        public event Action<SimpleServerChildTcpSocket> CloseAction;

        public void Dispose()
        {
            Reset();
            if (actionThread != null )
            {
                actionThread.Dispose();
                actionThread = null;
            }
        }

        private void Reset()
        {
            if (serverSocket != null) 
            {
                serverSocket.Dispose(); 
                serverSocket = null;
            }
        }        

        public MyServerSocket(int port)
        {
            Init(port);
        }

        private void Init(int port)
        {
            this.Port = port;
            actionThread = new ActionThread();
            actionThread.Start();
            actionThread.DoSynchronously(() =>
            {
                try
                {
                    serverSocket = new SimpleServerTcpSocket();
                    serverSocket.ConnectionArrived += ConnectionArrived;
                    serverSocket.Listen(this.Port);

                    Log.Logger.Information($"Listen - Port:{this.Port} ");
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"Listen error - Port:{this.Port}, Err:{ex.ToString()}");
                    Reset();
                }
                finally
                {
                    einit.Set();
                }
            });

            if (!einit.Wait(10000) || serverSocket == null)
            {
                throw new Exception("init fail");
            }
        }

        private void ConnectionArrived(AsyncResultEventArgs<SimpleServerChildTcpSocket> e)
        {
            if (e.Error != null)
            {
                if (e.UserState != null && (e.UserState.GetType() == typeof(ServerChildTcpSocket)))
                {
                    ServerChildTcpSocket conn = (ServerChildTcpSocket)e.UserState;
                    Log.Logger.Information($"Socket read error - {conn.RemoteEndPoint.ToString()}, Error:{e.Error.GetType()} / {e.Error.Message}");
                    conn.Close();
                }
                return;
            }

            SimpleServerChildTcpSocket s = e.Result;

            try
            {
                Log.Logger.Information($"Connection arrived - {s.RemoteEndPoint.ToString()}");
                AcceptAction?.Invoke(s);
                s.PacketArrived += (args) => ConnectionPacketArrived(s, args);
                s.WriteCompleted+= (args) => ConnectionWriteCompleted(s, args);
                s.ShutdownCompleted += (args) => ConnectionShutdownCompleted(s, args);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Accept connection error - {ex.ToString()}");
                Reset();
            }
        }

        private void ConnectionPacketArrived(SimpleServerChildTcpSocket client, AsyncResultEventArgs<byte[]> args)
        {
            try
            {
                if (args.Error != null || args.Result == null)
                {
                    client.Close();
                    return;
                }

                if (RecvAction == null)
                {
                    client.Close();
                }
                else
                {
                    RecvAction(client, args.Result);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Information($"Read error - {ex.ToString()}");
                client.Close();
            }
            
        }

        private void ConnectionWriteCompleted(SimpleServerChildTcpSocket client, AsyncCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                Log.Logger.Information($"Write error - {args.Error.GetType()} / {args.Error.Message} / {args.Error.HResult}");
                client.Close();
            }
            else
            {
                // send 성공이라 아무것도 안함
            }
        }

        private void ConnectionShutdownCompleted(SimpleServerChildTcpSocket client, AsyncCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                Log.Logger.Information($"Shutdown error - {args.Error.GetType()} / {args.Error.Message} / {args.Error.HResult}");
                client.Close();
            }
            else
            {
                Log.Logger.Information($"Shutdown completed - {client.RemoteEndPoint.ToString()}");
                client.Close();
            }
        }

        public void Stop()
        {
            Reset();
        }
    }
}
