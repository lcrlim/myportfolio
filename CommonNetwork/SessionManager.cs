using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 클라이언트 세션 관리자
    /// 브로드캐스트 및 특정 클라이언트에게 푸시 알림을 위한 기능을 제공.
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<long, IPacketParser> _sessions = new();

        /// <summary>
        /// 현재 연결된 세션 수
        /// </summary>
        public int SessionCount => _sessions.Count;

        /// <summary>
        /// 모든 연결된 세션 ID 목록
        /// </summary>
        public IEnumerable<long> SessionIds => _sessions.Keys;

        /// <summary>
        /// 세션 등록
        /// </summary>
        /// <param name="connectionId">연결 ID</param>
        /// <param name="parser">패킷 파서</param>
        public void Register(long connectionId, IPacketParser parser)
        {
            _sessions.TryAdd(connectionId, parser);
        }

        /// <summary>
        /// 세션 해제
        /// </summary>
        /// <param name="connectionId">연결 ID</param>
        public void Unregister(long connectionId)
        {
            _sessions.TryRemove(connectionId, out _);
        }

        /// <summary>
        /// 특정 세션이 존재하는지 확인
        /// </summary>
        /// <param name="connectionId">연결 ID</param>
        /// <returns>존재 여부</returns>
        public bool Contains(long connectionId)
        {
            return _sessions.ContainsKey(connectionId);
        }

        /// <summary>
        /// 특정 클라이언트에게 패킷 전송 (푸시)
        /// </summary>
        /// <param name="connectionId">대상 연결 ID</param>
        /// <param name="packet">전송할 패킷 (전송 후 자동 반환됨)</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>전송 성공 여부</returns>
        public async ValueTask<bool> SendToAsync(long connectionId, MyPacket packet, CancellationToken cancellationToken = default)
        {
            if (_sessions.TryGetValue(connectionId, out var parser))
            {
                await parser.EnqueuePacketAsync(packet, cancellationToken).ConfigureAwait(false);
                return true;
            }

            // 세션이 없으면 패킷 반환
            MyPacketPool.Return(packet);
            return false;
        }

        /// <summary>
        /// 모든 클라이언트에게 패킷 브로드캐스트
        /// 원본 패킷은 복사되어 각 클라이언트에게 전송됩니다.
        /// </summary>
        /// <param name="packet">전송할 패킷 (원본은 호출자가 반환해야 함)</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>전송된 클라이언트 수</returns>
        public async ValueTask<int> BroadcastAsync(MyPacket packet, CancellationToken cancellationToken = default)
        {
            int sentCount = 0;

            foreach (var (_, parser) in _sessions)
            {
                try
                {
                    // 각 세션에 패킷 복사본 전송
                    var copy = CopyPacket(packet);
                    await parser.EnqueuePacketAsync(copy, cancellationToken).ConfigureAwait(false);
                    sentCount++;
                }
                catch
                {
                    // 개별 전송 실패는 무시하고 계속 진행
                }
            }

            return sentCount;
        }

        /// <summary>
        /// 특정 클라이언트들에게 패킷 브로드캐스트
        /// </summary>
        /// <param name="connectionIds">대상 연결 ID 목록</param>
        /// <param name="packet">전송할 패킷 (원본은 호출자가 반환해야 함)</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>전송된 클라이언트 수</returns>
        public async ValueTask<int> BroadcastToAsync(IEnumerable<long> connectionIds, MyPacket packet, CancellationToken cancellationToken = default)
        {
            int sentCount = 0;

            foreach (var connectionId in connectionIds)
            {
                if (_sessions.TryGetValue(connectionId, out var parser))
                {
                    try
                    {
                        var copy = CopyPacket(packet);
                        await parser.EnqueuePacketAsync(copy, cancellationToken).ConfigureAwait(false);
                        sentCount++;
                    }
                    catch
                    {
                        // 개별 전송 실패는 무시하고 계속 진행
                    }
                }
            }

            return sentCount;
        }

        /// <summary>
        /// 특정 클라이언트를 제외한 모든 클라이언트에게 브로드캐스트
        /// </summary>
        /// <param name="excludeConnectionId">제외할 연결 ID</param>
        /// <param name="packet">전송할 패킷 (원본은 호출자가 반환해야 함)</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>전송된 클라이언트 수</returns>
        public async ValueTask<int> BroadcastExceptAsync(long excludeConnectionId, MyPacket packet, CancellationToken cancellationToken = default)
        {
            int sentCount = 0;

            foreach (var (connectionId, parser) in _sessions)
            {
                if (connectionId == excludeConnectionId)
                    continue;

                try
                {
                    var copy = CopyPacket(packet);
                    await parser.EnqueuePacketAsync(copy, cancellationToken).ConfigureAwait(false);
                    sentCount++;
                }
                catch
                {
                    // 개별 전송 실패는 무시하고 계속 진행
                }
            }

            return sentCount;
        }

        /// <summary>
        /// 패킷 복사 (브로드캐스트용)
        /// </summary>
        private static MyPacket CopyPacket(MyPacket source)
        {
            var copy = MyPacketPool.Rent();
            copy.Type = source.Type;
            copy.Len = source.Len;

            if (!source.BodyMemory.IsEmpty)
            {
                int bodyLength = source.BodyMemory.Length;
                var buffer = ArrayPool<byte>.Shared.Rent(bodyLength);
                source.BodyMemory.CopyTo(buffer);
                copy.BodyMemory = buffer.AsMemory(0, bodyLength);
                copy._rentedBuffer = buffer;
            }
            else
            {
                copy.BodyMemory = ReadOnlyMemory<byte>.Empty;
                copy._rentedBuffer = null;
            }

            return copy;
        }
    }
}
