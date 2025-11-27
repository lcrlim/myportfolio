using Microsoft.Extensions.DependencyInjection;
using MyCommonNet;
using Serilog;
using TcpServerStandard;

namespace NetStandardUnitTest
{
    [TestClass]
    public class UnitTestNetStandard
    {
        /// <summary>
        /// 핑퐁 tcp 테스트
        /// </summary>
        [TestMethod]
        public async Task TestPingPong()
        {
            var services = new ServiceCollection();

            // 핸들러들이 들어있는 어셈블리 (PacketPingHandler 기준)
            var handlerAssembly = typeof(PacketPingHandler).Assembly;
            services.AddPacketHandlersFromAssembly(handlerAssembly);
            services.AddSingleton<IPacketDispatcher>(sp => new PacketDispatcher(sp, handlerAssembly));

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = serviceProvider.GetRequiredService<IPacketDispatcher>();

            TcpServer server = new TcpServer();

            var cts = new CancellationTokenSource();
            var serverTask = server.Start(8888, dispatcher, cts.Token);
            Console.WriteLine($"Server started");

            using (var client = new TestClient())
            {
                await client.ConnectAsync("127.0.0.1", 8888);
                Console.WriteLine($"Connected");
                Console.WriteLine($"Ping - Num:3, Str:test test");
                var pong = await client.Ping(3, "test test");
                Console.WriteLine($"Pong - Num:{pong?.Num}, Str:{pong?.Str}");
                Assert.AreEqual(3, pong?.Num);
                Assert.AreEqual("test test", pong?.Str);
            }

            cts.Cancel();
            await serverTask;
        }
    }
}