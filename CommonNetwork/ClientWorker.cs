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

namespace CommonNetwork
{
    /// <summary>
    /// 클라이언트 작업자, 패킷 읽기 쓰기를 비동기로 처리
    /// </summary>
    public class ClientWorker
    {
        private TcpClient client;
        private CancellationToken ct;
        private PacketParser parser;
        private IPacketDispatcher dispatcher;

        public ClientWorker(TcpClient conn, IPacketDispatcher dispatcher, CancellationToken ctoken)
        {
            this.client = conn;
            this.ct = ctoken;
            this.parser = new PacketParser(conn.GetStream());
            this.dispatcher = dispatcher;
        }

        public async Task RunReadAsync()
        {
            try
            {
                using (var stream = client.GetStream())
                {   
                    // 데이터 읽기 반복
                    while (true)
                    {
                        // 패킷 읽기                        
                        PacketBase req = await parser.ReadPacketBase().ConfigureAwait(false);

                        // 여기서 데이터를 원하는 형태로 파싱하고 처리합니다.
                        PacketBase? res = await this.dispatcher.Dispatch(req).ConfigureAwait(false);
                        if (res != null)
                        {
                            await parser.WritePacketBase(res).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Log.Logger.Debug($"Connection closed");
            }
            catch (Exception ex)
            {
                Log.Logger.Warning($"Error ocurred - {ex.ToString()}");
            }
            finally
            {
                client.Dispose();
            }
        }
    }
}
