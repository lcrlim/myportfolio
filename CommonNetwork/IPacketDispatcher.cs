using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyCommonNet
{
    //public interface IPacketDispatcher
    //{
    //    public Task<MyPacket?> Dispatch(MyPacket req);
    //}

    /// <summary>
    /// Dispatcher 인터페이스
    /// </summary>
    public interface IPacketDispatcher
    {
        Task<MyPacket?> DispatchAsync(MyPacket request, CancellationToken cancellationToken = default);
    }
}
