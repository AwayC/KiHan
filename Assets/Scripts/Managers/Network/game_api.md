# 战斗服 (Game Server) 接口与协议文档

`game_server` 负责处理游戏内的核心战斗逻辑和帧同步驱动。它同时与 `lobby_server` (通过内网 gRPC 直接通信) 和客户端 (通过 Gateway 的 gRPC 双向流) 进行交互。

## 错误码定义
范围: -3000 ~ -3999

| 错误码 | 常量名 | 描述 |
| :--- | :--- | :--- |
| 0 | GAME_ERR_OK | 成功 |
| -3001 | GAME_ERR_ROOM_NOT_EXIST | 房间不存在 |
| -3002 | GAME_ERR_ROOM_FULL | 房间已满 |
| -3003 | GAME_ERR_PLAYER_NOT_IN_ROOM | 玩家不在房间内 |
| -3004 | GAME_ERR_INVALID_STATE | 状态不正确 (如游戏已开始仍发送准备) |

---

## 1. 跨服 RPC 接口 (Lobby <-> Game)
这些接口是服务端之间的内部调用，客户端无需关注。

### 1.1 `CreateRoom` (Lobby -> Game)
*   **描述**: 大厅服匹配成功后，请求战斗服创建一个独立的房间。
*   **请求 Payload (`CreateRoomReq`)**: 
    包含 `player_list_json` (JSON 格式的玩家列表字符串)。
*   **响应 Payload (`CreateRoomRsp`)**: 
    包含 `err_code` 和分配的 `room_id`。

### 1.2 `ReportGameResult` (Game -> Lobby)
*   **描述**: 战斗结束时，战斗服将结果汇报给大厅服，以便大厅服结算战绩（更新总场次、胜场等）。
*   **请求 Payload (`GameResultNtf`)**: 
    包含 `room_id`, `p1_uid` (uint32), `p2_uid` (uint32), 以及 `winner_uid` (uint32，0代表平局)。

---

## 2. 客户端接入协议 (Client <-> Gateway <-> Game)
客户端通过 Gateway 以 `CmdID >= 2000` 的包与 `game_server` 通信。底层走的是 gRPC 的 **双向流 (Bidirectional Streaming)** 或 WebSocket 等网关协议，客户端只需关注 `CmdID` 和对应的 `Payload`。

### 通用数据结构
```protobuf
message RoomSnapshot {
    int32 room_id = 1;
    // 包含 1P 和 2P 的完整信息 JSON 字符串，例如: 
    // [{"uid":12345, "nickname":"Player1", "character_id":1}, ...]
    string player_list_json = 2; 
}

message RoomFrame {
    uint32 frame_id = 1;
    uint32 player_count = 2;
    // key: game_id (1=1P, 2=2P)
    // value: 6 bytes raw input [FrameId(4,大端)][Joystick(1)][Buttons(1)]
    map<int32, bytes> raw_inputs = 3; 
}
```

### CmdID: 2001 - 进入房间
*   **请求: EnterRoomReq**
    *   **描述**: 客户端拿着大厅给的 `room_id` 请求进入战斗服房间。
    *   **Payload**: `message EnterRoomReq { int32 room_id = 1; }`
*   **响应: EnterRoomRsp**
    *   **描述**: 返回房间当前快照，同时分配客户端在房间内的 `game_id` (1P 还是 2P)。
    *   **Payload**: `message EnterRoomRsp { int32 err_code = 1; RoomSnapshot snapshot = 2; int32 my_game_id = 3; }`

### CmdID: 2002 - 玩家加载准备完毕
*   **请求: PlayerReadyReq**
    *   **描述**: 客户端资源加载完毕，发送 Ready。当房间内所有玩家均 Ready 时，服务器将广播游戏开始。
    *   **Payload**: `message PlayerReadyReq { int32 room_id = 1; }`
*   **响应: PlayerReadyRsp**
    *   **描述**: 准备状态确认。
    *   **Payload**: `message PlayerReadyRsp { int32 err_code = 1; }`

### CmdID: 2003 - 游戏开始通知 (Server 推送)
*   **通知: GameStartNtf**
    *   **描述**: 所有人准备完毕，服务器广播开始，同时开始以固定频率 (如 66ms) 下发逻辑帧。
    *   **Payload**: `message GameStartNtf { int32 room_id = 1; }`

### CmdID: 2004 - 客户端帧输入上报
*   **请求: PlayerFrameInput**
    *   **描述**: 客户端高频上报自己的操作帧。
    *   **Payload**: 
        ```protobuf
        message PlayerFrameInput { 
            // 固定 6 字节二进制: [FrameId(4,大端)][Joystick(1)][Buttons(1)]
            bytes raw_input = 1; 
        }
        ```
*   **响应**: 无直接响应，服务端收集整合后通过 2005 下发。

### CmdID: 2005 - 房间物理/逻辑帧广播 (Server 推送)
*   **通知: RoomFrameUpdate**
    *   **描述**: 服务器以 66ms (15FPS) 频率向房间内所有人广播整合后的操作帧。
    *   **Payload**: `message RoomFrameUpdate { RoomFrame frame = 1; }`

### CmdID: 2006 - 客户端上报游戏结束
*   **请求: GameOverReq**
    *   **描述**: 客户端本地逻辑判定游戏结束（如某方血量归零），向服务器上报结果。
    *   **Payload**: `message GameOverReq { int32 room_id = 1; uint32 winner_uid = 2; int32 p1_hp = 3; int32 p2_hp = 4; }`
*   **响应: GameOverRsp**
    *   **描述**: 服务端确认收到结算请求。
    *   **Payload**: `message GameOverRsp { int32 err_code = 1; }`

### CmdID: 2007 - 服务器裁定游戏结束通知 (Server 推送)
*   **通知: GameOverNtf**
    *   **描述**: 服务器收到结算请求，或某方异常掉线断开，强制结束游戏并广播结果。客户端收到此通知后应展示结算界面。
    *   **Payload**: `message GameOverNtf { int32 room_id = 1; uint32 winner_uid = 2; }`

---

## 3. 核心机制设计

1.  **掉线判定**:
    如果有一方在战斗时连接断开 (`Gateway` 触发 `ClientDisconnect`)，`game_server` 应直接广播 `GameOverNtf`，判定留下的玩家获胜，并上报给 Lobby 结算。
2.  **帧同步机制**:
    *   **Tick**: 服务器以固定间隔 (如 66ms) 执行 Tick。
    *   **收集**: 每帧收集玩家上报的 `PlayerFrameInput`。若玩家某帧无输入，则默认为空操作或复制上一帧输入。
    *   **广播**: Tick 触发时，封装 `RoomFrame` 并广播给房间内所有有效玩家。客户端需要根据收到的 `RoomFrameUpdate` 推进游戏逻辑。