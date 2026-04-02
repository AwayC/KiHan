层次,核心职责,实现方式
1. 网络通讯层 (KCP),负责发送/接收二进制指令包。,C# 封装 KCP 库，处理 UDP 丢包重传。
2. 帧同步管理器 (Lockstep),驱动逻辑帧前进，收集输入，处理延迟。,自定义 FixedUpdate 逻辑循环。
3. 确定性物理逻辑层 (Deterministic),计算位移、跳跃重力、受击状态。,完全手动实现，使用定点数或整数。
4. 判定系统 (Hitbox System),判定招式是否打中人、身体碰撞。,自定义矩形检测（非 Unity Physics）。
5. 动画状态机 (Frame Animator),根据逻辑帧切换 2D 序列帧图片。,手动代码控制 SpriteRenderer。
6. 表现插值层 (View Interpolation),让逻辑位移在视觉上更丝滑。,在 Update 中做平滑过渡。

暂时不使用定点数，使用本地保证平台统一
现在已经实现后端协议如下
``` c#
using System;

namespace Naruto.Server.Logic;

public enum ErrCode
{
    ok = 0, 
    RoomFull = 1, 
    RoomInvalid = 2, 
    UnknownError = 100,
}

public enum ClientOpCode : byte
{
    RoomEnterReq = 1, // + [4:uid][4:roomId][1:characterId][nick]
    PlayerFrameInput = 2, // + [6:playerInput]
    PlayerReadyReq = 3, // + null
    PlayerListReq = 4, // + null
}

public enum ServerOpCode : byte
{
    RoomEnterResp = 1, // + [1:ErrCode][4:roomId][1:gameId]
    PlayerListResp = 2, // + [1:ErrCode][userInfo]
    GameFrameUpdate = 3, // + [GameFrame]
    GameStartNtf = 4, // + [4:roomId]
    GameOveNtf = 5, // + [1:roomId]
    PlayerReadyResp = 6, // [1:ErrCode]
    PlayerJoinNtf = 7, // + [4:uid][1:gameId][1:characterId][nick]
    PlayerLeftNtf = 8, // + [4:uid]
}
```
这里说明，这里项目只实现最小demo，后端没有数据库，没有登录功能，所有uid，roomid由客户端提供
帧结构
``` c#
using System;
using System.Reflection.Emit;

namespace Naruto.Server.Logic;

[Flags]
public enum ButtonMask : byte
{
    None = 0,
    Attack = 1 << 0,
    Skill1 = 1 << 1,
    Skill2 = 1 << 2,
    Ultimate = 1 << 3,
    Substitution = 1 << 4, // 替身
    Secret = 1 << 5,       // 秘卷
    Summon = 1 << 6,        // 通灵
    other = 1 << 7,         // 预留
}

public class InputFrame // 6 bytes total
{
    public const int RawDataLength = 2; // 2 bytes of raw input data from client
    public const int DataLength = 6;
    
    public uint FrameId; // 4 bytes
    public byte JoyStickAngle; // 0-255

    public ButtonMask Buttons;
    
    // todo: 
    // public byte emojiId; // 0-255
}

public class RoomFrame
{
    public uint FrameId = 1; // 4 bytes
    public byte PlayerCount; // 1 byte, 0-255
    public Dictionary<byte, byte[]> InputFrames = []; // <gameId, InputFrame>, InputFrame 2 bytes, no frameId

    public int Serialize(byte[] buffer)
    {
        using var ms = new MemoryStream(buffer);
        using var writer = new BinaryWriter(ms);

        writer.Write((byte)ServerOpCode.GameFrameUpdate);

        writer.Write(FrameId);

        writer.Write((byte)InputFrames.Count);

        foreach (var kvp in InputFrames)
        {
            writer.Write(kvp.Key);   // GameId (1 byte)
            writer.Write(kvp.Value); // 2 bytes
        }

        // 总包长
        return (int)ms.Position;
    }
}
```
