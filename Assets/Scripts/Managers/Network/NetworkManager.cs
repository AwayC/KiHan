using UnityEngine;
using System;
using KiHan.Logic;

/// <summary>
/// 网络通信层基类
/// </summary>
public abstract class NetworkManager : UnitySingleton<NetworkManager>
{
    // 供上层逻辑订阅的通用事件
    public Action OnConnected;
    public Action<ServerOpCode, ArraySegment<byte>> OnOpCodeReceived;

    // 必须由子类实现的接口
    public abstract void Connect();
    public abstract void Send(byte[] data);
    public abstract bool Connected { get; }
}
