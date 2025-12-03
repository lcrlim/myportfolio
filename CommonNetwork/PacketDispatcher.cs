using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json; // System.Text.Json 사용
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 패킷 타입(int)과 해당 핸들러를 매핑하여 적절한 핸들러를 호출하는 역할
    /// 리플렉션과 Expression Tree를 사용하여 핸들러 호출 성능을 최적화
    /// </summary>
    public sealed class PacketDispatcher : IPacketDispatcher
    {
        // 패킷 타입별 핸들러 델리게이트를 저장하는 캐시
        private readonly ConcurrentDictionary<int, Func<MyPacket, CancellationToken, Task<MyPacket?>>> handlerCache = new();
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// 생성자: 서비스 프로바이더와 핸들러가 포함된 어셈블리를 받아 초기화합니다.
        /// </summary>
        public PacketDispatcher(IServiceProvider serviceProvider, params Assembly[] handlerAssemblies)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            if (handlerAssemblies == null || handlerAssemblies.Length == 0)
                throw new ArgumentException("At least one handler assembly must be specified.", nameof(handlerAssemblies));

            // 핸들러 등록 시작
            RegisterHandlers(handlerAssemblies);
        }

        /// <summary>
        /// 지정된 어셈블리 내의 모든 IPacketHandler 구현체를 찾아 캐시에 등록
        /// </summary>
        private void RegisterHandlers(Assembly[] handlerAssemblies)
        {
            foreach (var assembly in handlerAssemblies.Distinct())
            {
                // 추상 클래스나 인터페이스가 아닌, IPacketHandler<>를 구현한 구체 클래스만 검색
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

                    // 패킷 클래스에 정의된 PacketType 속성 조회
                    var packetTypeAttr = packetClrType.GetCustomAttribute<PacketTypeAttribute>();
                    if (packetTypeAttr == null)
                    {
                        Log.Logger.Warning(
                            "[PacketDispatcher] {PacketClrType} PacketTypeAttribute not found",
                            packetClrType.FullName);
                        continue;
                    }

                    int packetTypeCode = packetTypeAttr.Type;

                    // 핸들러 호출을 위한 고성능 델리게이트(Invoker) 생성
                    var invoker = BuildInvoker(handlerType, packetClrType);

                    if (handlerCache.TryAdd(packetTypeCode, invoker))
                    {
                        Log.Logger.Information(
                            "[PacketDispatcher] PacketType {PacketType} -> Handler {HandlerType} registered",
                            (Packet.PacketType)packetTypeCode, handlerType.FullName);
                    }
                    else
                    {
                        Log.Logger.Warning(
                            "[PacketDispatcher] PacketType {PacketType} Handler already registered",
                            (Packet.PacketType)packetTypeCode);
                    }
                }
            }
        }

        /// <summary>
        /// 특정 핸들러 타입과 패킷 타입에 맞는 호출 델리게이트를 생성
        /// Expression Tree를 사용하여 리플렉션 호출 오버헤드 제거
        /// </summary>
        private Func<MyPacket, CancellationToken, Task<MyPacket?>> BuildInvoker(Type handlerType, Type packetClrType)
        {
            // 1. 핸들러 인스턴스 가져오기
            object handlerInstance = serviceProvider.GetRequiredService(handlerType);

            // 2. HandleAsync 메서드 정보 조회
            var methodInfo = handlerType.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public);
            if (methodInfo == null)
                throw new InvalidOperationException($"Handler {handlerType.FullName} does not contain a HandleAsync method.");

            // 3. Expression Tree를 사용한 메서드 호출 컴파일
            // (typedPacket, ct) => handler.HandleAsync(typedPacket, ct) 형태의 람다 생성
            var paramPacket = Expression.Parameter(typeof(MyPacket), "packet");
            var paramToken = Expression.Parameter(typeof(CancellationToken), "ct");

            var paramTypedPacket = Expression.Parameter(packetClrType, "typedPacket");
            var callExpression = Expression.Call(Expression.Constant(handlerInstance), methodInfo, paramTypedPacket, paramToken);
            
            var lambda = Expression.Lambda(callExpression, paramTypedPacket, paramToken);
            var compiledHandler = lambda.Compile();

            // 최종 호출 래퍼 함수 반환
            return async (MyPacket raw, CancellationToken ct) =>
            {
                MyPacket? typedPacket = null;
                // 역직렬화 (Zero Allocation: System.Text.Json & Span 사용)
                try
                {
                    if (raw.BodyMemory.IsEmpty)
                    {
                        // Body가 없는 경우 (빈 패킷 혹은 레거시 문자열 Body 사용 시)
                        if (!string.IsNullOrEmpty(raw.Body))
                        {
                             typedPacket = (MyPacket?)JsonSerializer.Deserialize(raw.Body, packetClrType);
                        }
                        else
                        {
                             Log.Logger.Warning("Empty body for packet type {Type}", raw.Type);
                             return null;
                        }
                    }
                    else
                    {
                         // 메모리 뷰(Span)에서 직접 객체로 변환 (고성능)
                         typedPacket = (MyPacket?)JsonSerializer.Deserialize(raw.BodyMemory.Span, packetClrType);
                    }
                }
                catch (JsonException ex)
                {
                    Log.Logger.Warning("JSON Deserialize error for type {Type}: {Message}", raw.Type, ex.Message);
                    return null;
                }

                if (typedPacket == null) return null;

                // 헤더 정보 복원
                typedPacket.Len = raw.Len;
                typedPacket.Type = raw.Type;

                // 2. 핸들러 실행 (컴파일된 델리게이트 호출)
                Task<MyPacket?>? responseTask = null;
                try 
                {
                    // DynamicInvoke를 사용하여 컴파일된 델리게이트 실행
                    // (컴파일된 람다는 구체적인 패킷 타입을 인자로 받으므로 object로 캐스팅하여 호출)
                    responseTask = (Task<MyPacket?>?)compiledHandler.DynamicInvoke(typedPacket, ct);
                }
                catch(Exception ex)
                {
                    Log.Logger.Error(ex, "Handler invoke error");
                    return null;
                }

                if (responseTask == null)
                {
                    return null;
                }
                
                // 비동기 핸들러 완료 대기
                var responsePacket = await responseTask.ConfigureAwait(false);
                if (responsePacket == null)
                {
                    return null;
                }

                // 3. 응답 패킷 직렬화 (Zero Allocation)
                // 클라이언트로 보내야할 응답 패킷을 바이트 배열로 직렬화
                byte[] responseBytes = JsonSerializer.SerializeToUtf8Bytes(responsePacket, responsePacket.GetType());
                
                return new MyPacket
                {
                    Type = responsePacket.Type,
                    BodyMemory = responseBytes,
                    Body = null, // 문자열 Body는 생성하지 않음
                    Len = Packet.PACKET_HEADER_SIZE + responseBytes.Length // 헤더 크기 + 바디 크기
                };
            };
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
