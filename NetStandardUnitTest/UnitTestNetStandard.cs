using MyCommonNet;
using Serilog;

namespace NetStandardUnitTest
{
    [TestClass]
    public class UnitTestNetStandard
    {
        /// <summary>
        /// ÇÎÆþ tcp Å×½ºÆ®
        /// </summary>
        [TestMethod]
        public async Task TestPingPong()
        {
            TcpServer server = new TcpServer();

            var cts = new CancellationTokenSource();
            var serverTask = server.Start(8888, new TcpServerStandard.PacketDispatcher(), cts.Token);
            Console.WriteLine($"Server started");

            using (var client = new TestClient())
            {
                await client.ConnectAsync("127.0.0.1", 8888);
                Console.WriteLine($"Connected");
                Console.WriteLine($"Ping - Num:3, Str:test test");
                var pong = await client.Ping(3, "test test");
                Console.WriteLine($"Pong - Num:{pong.Num}, Str:{pong.Str}");
                Assert.AreEqual(3, pong.Num);
                Assert.AreEqual("test test", pong.Str);
            }

            cts.Cancel();
            await serverTask;
        }
    }
}