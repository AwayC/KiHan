using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using KiHan.Logic;
using KiHan.Network;

/// <summary>
/// 虚拟网络层，用于单机模拟
/// </summary>
public class VirtualNetworkManager : UnitySingleton<VirtualNetworkManager>, INetworkChannel
{
    private float _tickRate => GameConfig.LOGIC_TICK_TIME;
    private uint _frameId = 0;
    private Dictionary<byte, InputFrame> _inputQueue = new Dictionary<byte, InputFrame>();
    private Dictionary<byte, InputFrame> _lastInputs = new Dictionary<byte, InputFrame>(); // 缓存最后一次有效输入
    private bool _isRunning = false;

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
        
        _lastInputs.Clear();
        _inputQueue.Clear();
        
        // 模拟 EnterRoomRsp (2001)
        EnterRoomRsp rsp = new EnterRoomRsp 
        { 
            err_code = 0, 
            my_game_id = 1, 
            snapshot = new RoomSnapshot { room_id = 999, player_list_json = "{}" } 
        };
        OnMessageReceived?.Invoke(2001, EnterRoomRspExt.Serialize(rsp));
        
        // 启动模拟服务器行为
        _isRunning = true;
        _frameId = 0;
        StartCoroutine(ServerLoop());
    }

    public void SendMsg(ushort cmdId, byte[] data)
    {
        if (!_isRunning) return;

        if (cmdId == 2004) // PlayerFrameInput
        {
            // Now data is a Protobuf message, not raw bytes.
            // We only care about parsing the local 6-byte block for simulation.
            // Since this is just local simulation, we can quickly decode the protobuf wrapper.
            // Field 1 (bytes) -> tag is (1 << 3) | 2 = 10 (0x0A).
            // A minimal parser:
            //if (data.Length > 2 && data[0] == 0x0A)
            //{
            //    int len = data[1];
            //    if (data.Length >= 2 + len && len == 6)
            //    {
            //        InputFrame input = new InputFrame();
            //        input.FrameId = (uint)((data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5]);
            //        input.JoyStickAngle = data[6];
            //        input.Buttons = (ButtonMask)data[7];

            //        // 存入队列并备份到缓存
            //        _inputQueue[1] = input;
            //        _lastInputs[1] = input;
            //    }
            //}
            
            // raw data
            if(data.Length == 6)
            {
                InputFrame input = new InputFrame();
                input.FrameId = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
                input.JoyStickAngle = data[4];
                input.Buttons = (ButtonMask)data[5];

                // 存入队列并备份到缓存
                _inputQueue[1] = input;
                _lastInputs[1] = input;
            }
        }
    }

    public bool Connected => _isRunning;

    private IEnumerator ServerLoop()
    {
        yield return new WaitForSeconds(0.5f);
        
        // 模拟 GameStartNtf (2003)
        GameStartNtf startNtf = new GameStartNtf { room_id = 999 };
        // 序列化逻辑为了简单，在这里手动拼，或者如果它没有 Serialize 方法，就写一个
        // 因为我们在单机，可以直接绕过序列化触发逻辑，但为了通用，我们补全 Serialize
        
        // 我们需要 GameStartNtf 的序列化。这里简单造一个空的包
        // 更好的做法是我们在 VirtualNetworkManager 不走真实网络层，但如果是完全解耦，就应该补全 Serialize
        byte[] startData = new byte[0]; // 简化处理，实际上 LockstepManager 没有解析 GameStartNtf 的 payload 内容，只是触发 StartGame()
        OnMessageReceived?.Invoke(2003, startData);

        // 等待前端加载完成
        while (GameApp.Instance == null || !GameApp.Instance.IsGameRunning)
        {
            yield return null;
        }

        float nextTickTime = Time.realtimeSinceStartup;

        while (_isRunning)
        {
            if (Time.realtimeSinceStartup >= nextTickTime)
            {
                _frameId++;
                nextTickTime += _tickRate;

                // 构造 RoomFrameUpdate (2005)
                RoomFrame frame = new RoomFrame();
                frame.FrameId = _frameId;
                frame.InputFrames = new Dictionary<byte, InputFrame>();

                // --- 玩家 1 (自己) ---
                if (!_inputQueue.TryGetValue(1, out var input1))
                {
                    // 核心修复：如果没有新包，则沿用上一帧输入包
                    if (!_lastInputs.TryGetValue(1, out input1))
                    {
                        input1 = new InputFrame { FrameId = _frameId, JoyStickAngle = 255, Buttons = ButtonMask.None };
                    }
                }
                frame.InputFrames[1] = input1;

                // --- 玩家 2 (静止) ---
                InputFrame input2 = new InputFrame { FrameId = _frameId, JoyStickAngle = 255, Buttons = ButtonMask.None };
                frame.InputFrames[2] = input2;

                RoomFrameUpdate update = new RoomFrameUpdate { frame = frame };
                
                LockstepManager.Instance.InjectLocalFrame(frame);
                
                _inputQueue.Clear();
            }

            yield return null;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        StopAllCoroutines();
    }
}
