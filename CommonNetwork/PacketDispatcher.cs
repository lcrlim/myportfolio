using Microsoft.Extensions.DependencyInjection;
using MyCommonNet;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 리플렉션으로 핸들러를 찾아 Type(int) -> handler invoker 매핑을 캐시에 저장
    /// </summary>
    public sealed class PacketDispatcher : IPacketDispatcher
    {
        // 인스턴스 단위로 사용하는 ConcurrentDictionary
        private readonly ConcurrentDictionary<int, Func<MyPacket, CancellationToken, Task<MyPacket?>>> _handlerCache = new();
        private readonly IServiceProvider _serviceProvider;

        public PacketDispatcher(IServiceProvider serviceProvider, params Assembly[] handlerAssemblies)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            if (handlerAssemblies == null || handlerAssemblies.Length == 0)
                throw new ArgumentException("At least one handler assembly must be specified.", nameof(handlerAssemblies));

            // 이제 인스턴스 메서드
            RegisterHandlers(handlerAssemblies);
        }

        /// <summary>
        /// 핸들러 어셈블리들을 스캔하여 캐시에 등록
        /// </summary>
        private void RegisterHandlers(Assembly[] handlerAssemblies)
        {
            foreach (var assembly in handlerAssemblies.Distinct())
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
                    var handlerType = h.Type;
                    var handlerInterface = h.HandlerInterface!;
                    var packetClrType = handlerInterface.GetGenericArguments()[0];

                    // 패킷 타입에 붙은 PacketType Attribute 조회
                    var packetTypeAttr = packetClrType.GetCustomAttribute<PacketTypeAttribute>();
                    if (packetTypeAttr == null)
                    {
                        Log.Logger.Warning(
                            "[PacketDispatcher] {PacketClrType} does not have PacketTypeAttribute. Skipping.",
                            packetClrType.FullName);
                        continue;
                    }

                    int packetTypeCode = packetTypeAttr.Type;

                    // 인스턴스 메서드 호출
                    var invoker = BuildInvoker(handlerType, packetClrType);

                    if (_handlerCache.TryAdd(packetTypeCode, invoker))
                    {
                        Log.Logger.Information(
                            "[PacketDispatcher] PacketType {PacketType} -> Handler {HandlerType} registered.",
                            packetTypeCode, handlerType.FullName);
                    }
                    else
                    {
                        Log.Logger.Warning(
                            "[PacketDispatcher] PacketType {PacketType} Handler already registered.",
                            packetTypeCode);
                    }
                }
            }
        }

        /// <summary>
        /// handlerType / packetClrType 기반으로 Invoker 생성
        /// </summary>
        private Func<MyPacket, CancellationToken, Task<MyPacket?>> BuildInvoker(Type handlerType, Type packetClrType)
        {
            // 핸들러는 DI 컨테이너에서 꺼내서 사용 (async-only, stateless 권장)
            return async (MyPacket raw, CancellationToken ct) =>
            {
                if (raw.Body == null)
                {
                    Log.Logger.Warning("Body is null for packet type {Type}", raw.Type);
                    return null;
                }

                // Body JSON → 실제 패킷 타입으로 역직렬화
                var typedPacket = (MyPacket?)JsonConvert.DeserializeObject(raw.Body, packetClrType);
                if (typedPacket == null)
                {
                    Log.Logger.Warning("Failed to deserialize packet body for type {Type}", raw.Type);
                    return null;
                }

                // 헤더 정보 유지
                typedPacket.Len = raw.Len;
                typedPacket.Type = raw.Type;

                // 핸들러 인스턴스 DI로 Resolve
                var handler = _serviceProvider.GetRequiredService(handlerType);

                // IPacketHandler<TPacket>.HandleAsync 호출
                var handleMethod = handlerType.GetMethod(
                    "HandleAsync",
                    BindingFlags.Instance | BindingFlags.Public);

                if (handleMethod == null)
                    throw new InvalidOperationException(
                        $"Handler {handlerType.FullName} does not contain a HandleAsync method.");

                object? taskObj;
                var parameters = handleMethod.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType.IsAssignableFrom(packetClrType) &&
                    parameters[1].ParameterType == typeof(CancellationToken))
                {
                    taskObj = handleMethod.Invoke(handler, new object[] { typedPacket, ct });
                }
                else if (parameters.Length == 1 &&
                         parameters[0].ParameterType.IsAssignableFrom(packetClrType))
                {
                    // CancellationToken 없는 버전도 허용
                    taskObj = handleMethod.Invoke(handler, new object[] { typedPacket });
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Handler {handlerType.FullName}.HandleAsync has an invalid signature.");
                }

                var task = (Task<MyPacket?>)taskObj!;
                var responsePacket = await task.ConfigureAwait(false);

                if (responsePacket == null)
                    return null;

                // 응답 패킷을 다시 네트워크용 MyPacket으로 감싸기
                var bodyJson = JsonConvert.SerializeObject(responsePacket);
                var envelope = new MyPacket
                {
                    Type = responsePacket.Type,
                    Body = bodyJson,
                };

                // Len = 4바이트 Len + 4바이트 Type + Body 길이
                envelope.Len = 8 + Encoding.UTF8.GetByteCount(bodyJson);

                return envelope;
            };
        }

        /// <summary>
        /// 실제 Dispatch
        /// </summary>
        public async Task<MyPacket?> DispatchAsync(MyPacket request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_handlerCache.TryGetValue(request.Type, out var invoker))
                {
                    Log.Logger.Information("Undefined packet type - PacketType:{PacketType}", request.Type);
                    return null;
                }

                return await invoker(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Logger.Information("Packet dispatch canceled - Type:{PacketType}", request.Type);
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Dispatch error - PacketType:{PacketType}", request.Type);
                throw;
            }
        }
    }
}
