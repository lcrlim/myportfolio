using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using EnumsNET;
using FlatBuffers;
using Newtonsoft;
using System.Text.Json.Serialization;
using System.Text.Json;
using Newtonsoft.Json;

namespace TcpServerStandard
{
    public class WorkTask
    {
        private TcpClient client;
        private CancellationToken ct;

        public WorkTask(TcpClient conn, CancellationToken ctoken)
        {
            this.client = conn;
            this.ct = ctoken;
        }

        public async Task Run()
        {
            try
            {
                using (var s = client.GetStream())
                {
                    using (var sr = new StreamReader(s))
                    using (var sw = new StreamWriter(s) { AutoFlush = true })
                    {
                        while (client.Connected && !ct.IsCancellationRequested)
                        {
                            char[] header = new char[Packet.PACKET_HEADER_SIZE];
                            char[] body;
                            // 타입 4바이트 + 길이 4바이트 + body, 최소 8바이트 읽은 후 다시 읽기
                            int readSize = await sr.ReadBlockAsync(header, 0, Packet.PACKET_HEADER_SIZE).ConfigureAwait(false);
                            if (readSize > 0)
                            {
                                Memory<char> len = new Memory<char>(header, 0, Packet.PACKET_HEADER_LEN_SIZE);
                                Memory<char> type = new Memory<char>(header, Packet.PACKET_HEADER_LEN_SIZE, Packet.PACKET_HEADER_TYPE_SIZE);
                                if (!Int32.TryParse(len.ToString(), out int packetLen) || !Int32.TryParse(len.ToString(), out int packetTypeInt))
                                {
                                    Log.Logger.Information("Invalid packet len or type");
                                    throw new InvalidDataException();
                                }

                                if (packetLen > Packet.PACKET_MAX_SIZE)
                                {
                                    Log.Logger.Information("Packet size too large");
                                    throw new InvalidDataException();
                                }

                                if (!Enums.TryParse<Packet.Type>(packetTypeInt.ToString(), out Packet.Type packetType))
                                {
                                    Log.Logger.Information($"Undefined packet type - {packetTypeInt}");
                                    throw new InvalidDataException();
                                }

                                body = new char[packetLen - Packet.PACKET_HEADER_SIZE];
                                readSize += await sr.ReadBlockAsync(body, 0, body.Length).ConfigureAwait(false);

                                // 다 읽은 body를 serializer에 넘기고 나온 패킷을 처리 로직으로 넘긴다.
                                PacketBase req = PacketParser.Parse(body);

                                char[]? res = ProcessPacket.Process(packetTypeInt, req);
                                if (res != null) 
                                {
                                    // 응답 헤더 생성 

                                    // 바디와 함께 전송
                                    await sw.WriteAsync(res, 0, res.Length).ConfigureAwait(false);
                                }
                            }

                            // 심플 에코 (받은 데이터 그대로 보내기 반복)
                            //var data = await sr.ReadLineAsync(ct).ConfigureAwait(false);
                            //if (data == null)
                            //{
                            //    Log.Information($"Connection closed");
                            //    break;
                            //}

                            //Log.Information($"Received : \"{data}\"");
                            //await sw.WriteLineAsync(data).ConfigureAwait(false);
                            //Log.Information($"Sent : \"{data}\"");
                        }
                    }
                }
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
