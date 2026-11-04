using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using KiHan.Logic;
using KiHan.Network;

/// <summary>
/// 虚拟网络层 (单机 / 训练场的本地服务器)：
/// - 进房 (2001) 后延时下发开局 (2003)
/// - 收到本机某一帧的输入 (2004) 立即合成该帧的确认帧递交给 LockstepManager：
///   玩家 1 = 本机输入，玩家 2 = 训练场敌人 (面板按住的按键 + 人机)
/// 节奏由客户端 LockstepManager 的定时器决定，本地服务器不计时；确认帧对象循环复用，局内不分配内存
/// </summary>
public class VirtualNetworkManager : UnitySingleton<VirtualNetworkManager>, INetworkChannel
{
    private const byte LocalSlot = 1;
    private const byte DummySlot = 2;

    /// <summary>
    /// 复用的确认帧数：本地服务器即时回包，待执行的帧不超过输入延迟 + 1 帧，留足余量
    /// </summary>
    private const int FramePoolSize = 8;

    private bool _isRunning;
    private readonly RoomFrame[] _framePool = new RoomFrame[FramePoolSize];
    private int _framePoolNext;

    public event Action<ushort, byte[]> OnMessageReceived;

    public bool IsConnected => _isRunning;

    public class EnterRoomRspExt
    {
        // 临时提供序列化解决单机测试报错
        public static byte[] Serialize(EnterRoomRsp rsp)
        {
            ProtoWriter writer = new ProtoWriter();
            if (rsp.err_code != 0) writer.WriteVarint(1, rsp.err_code);
            // Ignore other fields for local simulation 
            return writer.ToArray();
        }
    }

    public void Connect()
    {
        Debug.Log("[Net] Virtual Mode: Simulating Connection...");
        _isRunning = true;

        // 模拟 EnterRoomRsp (2001)
        EnterRoomRsp rsp = new EnterRoomRsp
        {
            err_code = 0,
            my_game_id = LocalSlot,
            snapshot = new RoomSnapshot { room_id = 999, player_list_json = "{}" }
        };
        OnMessageReceived?.Invoke(2001, EnterRoomRspExt.Serialize(rsp));

        StartCoroutine(StartGameLater());
    }

    public void SendMsg(ushort cmdId, byte[] data)
    {
        if (!_isRunning) return;

        // PlayerFrameInput 原始 6 字节：[FrameId(4,大端)][Joystick(1)][Buttons(1)]
        if (cmdId == 2004 && data.Length == InputFrame.DataLength)
        {
            uint frameId = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
            LockstepManager.Instance.InjectLocalFrame(BuildFrame(frameId, data[4], (ButtonMask)data[5]));
        }
    }

    public void Stop()
    {
        _isRunning = false;
        StopAllCoroutines();
    }

    private IEnumerator StartGameLater()
    {
        yield return new WaitForSeconds(0.5f);

        // 模拟 GameStartNtf (2003)：LockstepManager 不解析其内容，只触发 StartGame
        OnMessageReceived?.Invoke(2003, Array.Empty<byte>());
    }

    private RoomFrame BuildFrame(uint frameId, byte localDir, ButtonMask localButtons)
    {
        RoomFrame frame = _framePool[_framePoolNext];
        if (frame == null)
        {
            frame = new RoomFrame { InputFrames = new Dictionary<byte, InputFrame>(2) };
            frame.InputFrames[LocalSlot] = new InputFrame();
            frame.InputFrames[DummySlot] = new InputFrame();
            _framePool[_framePoolNext] = frame;
        }
        _framePoolNext = (_framePoolNext + 1) % FramePoolSize;

        frame.FrameId = frameId;

        InputFrame local = frame.InputFrames[LocalSlot];
        local.FrameId = frameId;
        local.JoyStickDir = localDir;
        local.Buttons = localButtons;

        // 玩家 2 (训练场敌人)：面板按住的按键 + 人机 (敌人攻击)，未开启时静止
        InputFrame dummy = frame.InputFrames[DummySlot];
        dummy.FrameId = frameId;
        dummy.JoyStickDir = JoystickDir.None;
        dummy.Buttons = ButtonMask.None;
        BattleManager.Instance.Training?.FillDummyInput(frameId, dummy);
        return frame;
    }
}
