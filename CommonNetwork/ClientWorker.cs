using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using System.Text.Json.Serialization;
using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.ObjectPool;

namespace MyCommonNet
{
    /// <summary>
    /// 클라이언트 작업자, 패킷 읽기 쓰기를 비동기로 처리
    /// </summary>
    public class ClientWorker
    {
        private long connId;
        private TcpClient? client;
        private CancellationToken? ct;
        private PacketParser parser;
        private IPacketDispatcher? dispatcher;

        public ClientWorker() => this.parser = new PacketParser();

        public ClientWorker(long connectionId, TcpClient conn, IPacketDispatcher dispatcher, CancellationToken ctoken)
        {
            this.connId = connectionId;
            this.client = conn;
            this.ct = ctoken;
            this.parser = new PacketParser(conn.GetStream());
            this.dispatcher = dispatcher;
        }

        public void Reset()
        {
            if (client != null)
            {
                try 
                { 
                    client.Dispose(); 
                } catch { }
            }
            this.connId = 0;
            this.client = null;
            this.ct = null;
            this.dispatcher = null;
            this.parser.ResetStream();
        }

        public void SetClient(long connectionId, TcpClient conn, IPacketDispatcher dispatcher, CancellationToken ctoken)
        {
            this.connId = connectionId;
            this.client = conn;
            this.ct = ctoken;
            this.dispatcher = dispatcher;
            this.parser.SetStream(conn.GetStream());
        }

        public async Task RunReadAsync(ObjectPool<ClientWorker> pool)
        {
            bool added = false;
            try
            {
                ServerMetrics.IncrementConnectionCount();
                added = true;

                if (client == null || dispatcher == null)
                {
                    Log.Logger.Error("Client or dispatcher is null in ClientWorker(Id:{ConnId})", connId);
                    pool.Return(this);
                    return;
                }

                using (var stream = client.GetStream())
                {   
                    // 데이터 읽기 반복
                    while (true)
                    {
                        // 패킷 읽기                        
                        MyPacket req = await parser.ReadPacket().ConfigureAwait(false);
                        ServerMetrics.IncrementPacketCount();

                        // 여기서 데이터를 원하는 형태로 파싱하고 처리합니다.
                        MyPacket? res = await this.dispatcher.DispatchAsync(req).ConfigureAwait(false);
                        if (res != null)
                        {
                            await parser.WritePacket(res).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Log.Logger.Information("Connection(Id:{ConnId}) closed", connId);
            }
            catch (SocketException ex)
            {
                // 소켓 관련 에러 (연결 끊김 등)
                Log.Logger.Information("Connection(Id:{ConnId}) closed by socket error: {SocketErrorCode}", this.connId, ex.SocketErrorCode);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning("Connection(Id:{ConnId}) closed by error - {Message}", this.connId, ex.Message);
            }
            finally
            {
                if (added)
                {
                    ServerMetrics.DecrementConnectionCount();
                }
                pool.Return(this);
            }
        }
    }
}
