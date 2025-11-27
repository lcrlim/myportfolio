using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    public static class PacketHandlerServiceCollectionExtensions
    {
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
                services.AddTransient(h.HandlerInterface!, h.Type);

                // 2) 구현 타입 자체도 등록
                services.AddTransient(h.Type);
            }

            return services;
        }
    }
}
