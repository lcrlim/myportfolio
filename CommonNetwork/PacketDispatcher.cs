using Microsoft.Extensions.DependencyInjection;
using MyCommonNet.Serialization;
using Serilog;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 타입(int)과 해당 핸들러를 매핑하여 적절한 핸들러를 호출하는 역할
    /// 리플렉션과 Expression Tree를 사용하여 핸들러 호출 성능을 최적화
    /// ISerializer를 통한 직렬화 추상화 및 응답 패킷 풀링 지원
    /// </summary>
    public sealed class PacketDispatcher : IPacketDispatcher
    {
        // 패킷 타입별 핸들러 델리게이트를 저장하는 캐시
        private readonly ConcurrentDictionary<int, Func<MyPacket, CancellationToken, Task<MyPacket?>>> handlerCache = new();
        private readonly IServiceProvider serviceProvider;
        private readonly ISerializer serializer;

        /// <summary>
        /// 생성자: 서비스 프로바이더, 직렬화기, 핸들러 어셈블리를 받아 초기화.
        /// </summary>
        /// <param name="serviceProvider">DI 서비스 프로바이더</param>
        /// <param name="serializer">직렬화기 (JSON, MessagePack 등)</param>
        /// <param name="handlerAssemblies">핸들러가 포함된 어셈블리</param>
        public PacketDispatcher(IServiceProvider serviceProvider, ISerializer serializer, params Assembly[] handlerAssemblies)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            if (handlerAssemblies == null || handlerAssemblies.Length == 0)
                throw new ArgumentException("At least one handler assembly must be specified.", nameof(handlerAssemblies));

            Log.Information("[PacketDispatcher] 직렬화기: {SerializerName}", serializer.FormatName);

            // 핸들러 등록 시작
            RegisterHandlers(handlerAssemblies);
        }

        /// <summary>
        /// 생성자: 서비스 프로바이더와 핸들러 어셈블리만 받는 오버로드 (기존 호환성)
        /// JSON 직렬화기를 기본으로 사용
        /// </summary>
        [Obsolete("ISerializer를 명시적으로 전달하는 생성자를 사용하세요.")]
        public PacketDispatcher(IServiceProvider serviceProvider, params Assembly[] handlerAssemblies)
            : this(serviceProvider, SerializerFactory.CreateDefault(), handlerAssemblies)
        {
        }

        /// <summary>
        /// 지정된 어셈블리 내의 모든 IPacketHandler 구현체를 찾아 캐시에 등록
        /// </summary>
        private void RegisterHandlers(Assembly[] handlerAssemblies)
        {
            foreach (var assembly in handlerAssemblies.Distinct())
            {
                var handlerTypes = FindHandlerTypes(assembly);

                foreach (var h in handlerTypes)
                {
                    RegisterHandler(h.Type, h.HandlerInterface!, h.PacketClrType);
                }
            }
        }

        /// <summary>
        /// 어셈블리에서 IPacketHandler<T>를 구현한 타입을 찾습니다
        /// </summary>
        private static IEnumerable<(Type Type, Type? HandlerInterface, Type PacketClrType)> FindHandlerTypes(Assembly assembly)
        {
            return assembly
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
                .Where(x => x.HandlerInterface != null)
                .Select(x => (x.Type, x.HandlerInterface, x.HandlerInterface!.GetGenericArguments()[0]));
        }

        /// <summary>
        /// 단일 핸들러를 등록
        /// </summary>
        private void RegisterHandler(Type handlerType, Type handlerInterface, Type packetClrType)
        {
            // 패킷 클래스에 정의된 PacketType 속성 조회
            var packetTypeAttr = packetClrType.GetCustomAttribute<PacketTypeAttribute>();
            if (packetTypeAttr == null)
            {
                Log.Warning(
                    "[PacketDispatcher] {PacketClrType} PacketTypeAttribute not found",
                    packetClrType.FullName);
                return;
            }

            int packetTypeCode = packetTypeAttr.Type;

            // 핸들러 호출을 위한 고성능 델리게이트(Invoker) 생성
            var invoker = BuildInvoker(handlerType, packetClrType);

            if (handlerCache.TryAdd(packetTypeCode, invoker))
            {
                Log.Information(
                    "[PacketDispatcher] PacketType {PacketType} -> Handler {HandlerType} registered",
                    (Packet.PacketType)packetTypeCode, handlerType.FullName);
            }
            else
            {
                Log.Warning(
                    "[PacketDispatcher] PacketType {PacketType} Handler already registered",
                    (Packet.PacketType)packetTypeCode);
            }
        }

        /// <summary>
        /// 특정 핸들러 타입과 패킷 타입에 맞는 호출 델리게이트를 생성
        /// </summary>
        private Func<MyPacket, CancellationToken, Task<MyPacket?>> BuildInvoker(Type handlerType, Type packetClrType)
        {
            var compiledHandler = BuildExpressionTreeHandler(handlerType, packetClrType);

            return async (MyPacket raw, CancellationToken ct) =>
            {
                // 역직렬화
                var typedPacket = DeserializePacket(raw, packetClrType);
                if (typedPacket == null)
                    return null;

                // 핸들러 실행
                var responsePacket = await ExecuteHandlerAsync(compiledHandler, typedPacket, ct);
                if (responsePacket == null)
                    return null;

                // 응답 직렬화
                return SerializeResponse(responsePacket);
            };
        }

        /// <summary>
        /// Expression Tree를 사용하여 핸들러 호출 델리게이트를 컴파일
        /// DynamicInvoke 없이 직접 호출 가능하도록 최적화
        /// </summary>
        private Func<object, CancellationToken, Task<MyPacket?>> BuildExpressionTreeHandler(Type handlerType, Type packetClrType)
        {
            // 핸들러 인스턴스 가져오기
            object handlerInstance = serviceProvider.GetRequiredService(handlerType);

            // HandleAsync 메서드 정보 조회
            var methodInfo = handlerType.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public);
            if (methodInfo == null)
                throw new InvalidOperationException($"Handler {handlerType.FullName} does not contain a HandleAsync method.");

            // Expression Tree를 사용한 강타입 델리게이트 생성
            var paramPacket = Expression.Parameter(typeof(object), "packet");
            var paramToken = Expression.Parameter(typeof(CancellationToken), "ct");

            // object -> 구체적인 패킷 타입으로 변환
            var convertedPacket = Expression.Convert(paramPacket, packetClrType);
            var callExpression = Expression.Call(Expression.Constant(handlerInstance), methodInfo, convertedPacket, paramToken);

            var lambda = Expression.Lambda<Func<object, CancellationToken, Task<MyPacket?>>>(
                callExpression,
                paramPacket,
                paramToken);

            return lambda.Compile();
        }

        /// <summary>
        /// 패킷을 역직렬화
        /// </summary>
        private MyPacket? DeserializePacket(MyPacket raw, Type packetClrType)
        {
            try
            {
                var typedPacket = (MyPacket?)serializer.Deserialize(raw.BodyMemory.Span, packetClrType);
                if (typedPacket == null)
                    return null;

                // 헤더 정보 복원
                typedPacket.Len = raw.Len;
                typedPacket.Type = raw.Type;
                return typedPacket;
            }
            catch (Exception ex)
            {
                Log.Warning("Deserialize error - Type: {Type}, Error: {Message}", raw.Type, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 핸들러를 실행
        /// </summary>
        private async Task<MyPacket?> ExecuteHandlerAsync(
            Func<object, CancellationToken, Task<MyPacket?>> compiledHandler,
            MyPacket typedPacket,
            CancellationToken ct)
        {
            try
            {
                return await compiledHandler(typedPacket, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Packet handler error");
                return null;
            }
        }

        /// <summary>
        /// 응답 패킷을 직렬화
        /// </summary>
        private MyPacket? SerializeResponse(MyPacket responsePacket)
        {
            MyPacket result = MyPacketPool.Rent();
            result.Type = responsePacket.Type;

            try
            {
                // IBufferWriter를 사용하여 직렬화 (Zero-copy)
                // MyPacket이 IBufferWriter<byte>를 구현하므로 복사 없이 직접 씀
                serializer.Serialize(responsePacket, (IBufferWriter<byte>)result);
                
                // 직렬화 결과 바인딩
                result.BodyMemory = result.WrittenMemory;
                result.Len = Packet.PACKET_HEADER_SIZE + result.WriteBufferLength;

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Response packet serialize error");
                MyPacketPool.Return(result);
                return null;
            }
        }

        /// <summary>
        /// 수신된 패킷을 적절한 핸들러에게 전달 후 응답 패킷 반환
        /// </summary>
        public async Task<MyPacket?> DispatchAsync(MyPacket request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!handlerCache.TryGetValue(request.Type, out var invoker))
                {
                    Log.Information("Undefined packet type - PacketType:{PacketType}", request.Type);
                    return null;
                }

                return await invoker(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Packet dispatch canceled - Type:{PacketType}", request.Type);
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Dispatch error - PacketType:{PacketType}", request.Type);
                throw;
            }
        }
    }
}
