using Microsoft.Extensions.DependencyInjection;
using MyCommonNet;
using MyCommonNet.Serialization;
using Serilog;

namespace TcpServerStandard
{
    /// <summary>
    /// DI 컨테이너 구성을 담당
    /// </summary>
    public static class DependencyInjectionSetup
    {
        /// <summary>
        /// 서비스 컬렉션을 구성하고 ServiceProvider를 반환
        /// </summary>
        /// <param name="options">서버 옵션</param>
        /// <returns>구성된 ServiceProvider</returns>
        public static IServiceProvider ConfigureServices(TcpServerOptions options)
        {
            var services = new ServiceCollection();

            // Serilog Logger 주입
            services.AddSingleton(Log.Logger);

            // 핸들러들이 들어있는 어셈블리 (PacketPingHandler가 정의된 어셈블리)
            var handlerAssembly = typeof(PacketPingHandler).Assembly;

            // IPacketHandler<T> 구현 클래스들을 한 번에 등록
            services.AddPacketHandlersFromAssembly(handlerAssembly);

            // ISerializer 등록 (옵션에서 지정된 직렬화 방식 사용)
            services.AddSerializerFromOptions(options);

            // PacketDispatcher 등록 (ISerializer 주입)
            services.AddSingleton<IPacketDispatcher>(sp =>
            {
                var serializer = sp.GetRequiredService<ISerializer>();
                return new PacketDispatcher(sp, serializer, handlerAssembly);
            });

            return services.BuildServiceProvider();
        }
    }
}
