using System;

namespace KiHan.Network
{
    /// <summary>
    /// 网络通道抽象接口，用于解耦具体网络实现（单机虚拟网 / 联机真实网关）
    /// </summary>
    public interface INetworkChannel
    {
        bool IsConnected { get; }
        
        /// <summary>
        /// 发送协议数据包
        /// </summary>
        void SendMsg(ushort cmdId, byte[] payload);

        /// <summary>
        /// 接收协议数据包事件
        /// </summary>
        event Action<ushort, byte[]> OnMessageReceived;
    }
}
