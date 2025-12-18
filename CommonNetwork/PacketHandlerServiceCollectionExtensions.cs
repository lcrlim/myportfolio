using Microsoft.Extensions.DependencyInjection;
using MyCommonNet.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 핸들러 및 직렬화 관련 DI 확장 메서드
    /// </summary>
    public static class PacketHandlerServiceCollectionExtensions
    {
        /// <summary>
        /// 어셈블리에서 IPacketHandler 구현체를 찾아 DI 컨테이너에 등록
        /// </summary>
        /// <param name="services">서비스 컬렉션</param>
        /// <param name="assembly">핸들러가 포함된 어셈블리</param>
        /// <returns>서비스 컬렉션</returns>
        public static IServiceCollection AddPacketHandlersFromAssembly(
            this IServiceCollection services,
            Assembly assembly)
        {
            var handlerTypes = assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Select(t => new
                {
                    Type = t,
                    HandlerInterface = t.GetInterfaces()
                        .FirstOrDefault(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IPacketHandler<>))
                })
                .Where(x => x.HandlerInterface != null);

            foreach (var h in handlerTypes)
            {
                // 1) IPacketHandler<T> 구현 타입 등록
                services.AddSingleton(h.HandlerInterface!, h.Type);

                // 2) 구현 타입 자체도 등록
                services.AddSingleton(h.Type);
            }

            return services;
        }

        /// <summary>
        /// 직렬화기를 DI 컨테이너에 등록
        /// </summary>
        /// <param name="services">서비스 컬렉션</param>
        /// <param name="type">직렬화 타입 (기본값: JSON)</param>
        /// <returns>서비스 컬렉션</returns>
        public static IServiceCollection AddSerializer(
            this IServiceCollection services,
            SerializerType type = SerializerType.Json)
        {
            services.AddSingleton<ISerializer>(_ => SerializerFactory.Create(type));
            return services;
        }

        /// <summary>
        /// 커스텀 직렬화기를 DI 컨테이너에 등록
        /// </summary>
        /// <typeparam name="TSerializer">ISerializer 구현 타입</typeparam>
        /// <param name="services">서비스 컬렉션</param>
        /// <returns>서비스 컬렉션</returns>
        public static IServiceCollection AddSerializer<TSerializer>(this IServiceCollection services)
            where TSerializer : class, ISerializer
        {
            services.AddSingleton<ISerializer, TSerializer>();
            return services;
        }

        /// <summary>
        /// TcpServerOptions에 기반한 직렬화기를 DI 컨테이너에 등록
        /// </summary>
        /// <param name="services">서비스 컬렉션</param>
        /// <param name="options">TCP 서버 옵션</param>
        /// <returns>서비스 컬렉션</returns>
        public static IServiceCollection AddSerializerFromOptions(
            this IServiceCollection services,
            TcpServerOptions options)
        {
            return services.AddSerializer(options.SerializerType);
        }
    }
}
