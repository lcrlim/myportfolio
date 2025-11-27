using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    /// <summary>
    /// 타입별 패킷 핸들러 인터페이스 (async-only)
    /// </summary>
    public interface IPacketHandler<TPacket> where TPacket : MyPacket
    {
        /// <summary>
        /// 패킷 처리. 반드시 비동기 I/O만 수행 (Task.Run/Thread.Sleep 금지).
        /// </summary>
        Task<MyPacket?> HandleAsync(TPacket packet, CancellationToken cancellationToken = default);
    }
}
