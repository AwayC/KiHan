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
    *   **描述**: 客户端在选人界面确认阵容后发送 (紧跟 2010 之后)。当房间内所有玩家均 Ready 时，服务器将广播游戏开始。
    *   **Payload**: `message PlayerReadyReq { int32 room_id = 1; }`
*   **响应: PlayerReadyRsp**
    *   **描述**: 准备状态确认。
    *   **Payload**: `message PlayerReadyRsp { int32 err_code = 1; }`

### CmdID: 2003 - 游戏开始通知 (Server 推送)
*   **通知: GameStartNtf**
    *   **描述**: 所有人准备完毕，服务器广播开始，同时开始以固定频率 (1/30 秒，约 33.3ms) 下发逻辑帧。
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
    *   **描述**: 服务器以约 33.3ms (30FPS) 频率向房间内所有人广播整合后的操作帧。
    *   **Payload**: `message RoomFrameUpdate { RoomFrame frame = 1; }`

### CmdID: 2006 - 客户端上报游戏结束
*   **请求: GameOverReq**
    *   **描述**: 客户端本地逻辑判定游戏结束（如某方血量归零），向服务器上报结果。
    *   **Payload**: `message GameOverReq { int32 room_id = 1; uint32 winner_uid = 2; int32 p1_hp = 3; int32 p2_hp = 4; int32 winner_game_id = 5; }`
    *   **说明 (新增 `winner_game_id`)**: 3v3 车轮战整场结束时上报，获胜方 `game_id` (1 / 2)，0 为平局；客户端不知道对方 uid，`winner_uid` 不再填写，由服务器按 game_id 映射。`p1_hp` / `p2_hp` 为最后一回合双方剩余血量。
*   **响应: GameOverRsp**
    *   **描述**: 服务端确认收到结算请求。
    *   **Payload**: `message GameOverRsp { int32 err_code = 1; }`

### CmdID: 2007 - 服务器裁定游戏结束通知 (Server 推送)
*   **通知: GameOverNtf**
    *   **描述**: 服务器收到结算请求，或某方异常掉线断开，强制结束游戏并广播结果。客户端已显示整场结算时忽略；对局未结束 (如对方掉线) 时提示并回到大厅。
    *   **Payload**: `message GameOverNtf { int32 room_id = 1; uint32 winner_uid = 2; }`

### CmdID: 2008 - 发送表情 (新增，服务器待实现)
*   **请求: EmojiReq**
    *   **描述**: 战斗中发送表情。服务器不做逻辑处理，直接以 2009 广播给房间内所有玩家 (含发送者)。
    *   **Payload**: `message EmojiReq { int32 room_id = 1; int32 emoji_id = 2; }` (`emoji_id` 为表情面板中的序号，从 0 开始)
*   **响应**: 无。

### CmdID: 2009 - 表情广播 (Server 推送，新增)
*   **通知: EmojiNtf**
    *   **Payload**: `message EmojiNtf { int32 game_id = 1; int32 emoji_id = 2; }` (`game_id` 为发送者)
    *   **说明**: 表情不进入帧同步，不影响逻辑；客户端可屏蔽对方表情。

### CmdID: 2010 - 提交出战阵容 (新增，服务器待实现)
*   **请求: LineupReq**
    *   **描述**: 进房 (2001) 后客户端打开选人界面，确认后发送阵容 (按出战顺序 3 名忍者及各自佩戴的秘卷)，随后发送 2002。
    *   **Payload**:
        ```protobuf
        message LineupMember { int32 character_id = 1; string secret = 2; } // secret 为空表示不佩戴
        message LineupReq { int32 room_id = 1; repeated LineupMember members = 2; }
        ```
*   **响应**: 无 (双方都提交后服务器广播 2011)。

### CmdID: 2011 - 双方阵容广播 (Server 推送，新增)
*   **通知: LineupNtf**
    *   **描述**: 房间内所有玩家都提交阵容后广播，**必须早于 2003 GameStartNtf**；客户端收到后进入阵容 Loading，开局时按阵容生成角色 (双端一致)。
    *   **Payload**:
        ```protobuf
        message PlayerLineup { int32 game_id = 1; repeated LineupMember members = 2; }
        message LineupNtf { repeated PlayerLineup lineups = 1; }
        ```

### 延迟显示 (前端接口)
*   战斗界面显示双方延迟 (ms)。客户端预留 `LockstepManager.ReportPing(game_id, ms)` → `OnPingUpdated`，后端提供测量方式 (如 ping/pong 协议或在 2005 中附带) 后接入；未接入时界面显示 `--`。

---

## 3. 核心机制设计

1.  **掉线判定**:
    如果有一方在战斗时连接断开 (`Gateway` 触发 `ClientDisconnect`)，`game_server` 应直接广播 `GameOverNtf`，判定留下的玩家获胜，并上报给 Lobby 结算。
2.  **帧同步机制**:
    *   **Tick**: 服务器以固定间隔 (1/30 秒，约 33.3ms) 执行 Tick。
    *   **收集**: 每帧收集玩家上报的 `PlayerFrameInput`。若玩家某帧无输入，则默认为空操作或复制上一帧输入。
    *   **广播**: Tick 触发时，封装 `RoomFrame` 并广播给房间内所有有效玩家。客户端需要根据收到的 `RoomFrameUpdate` 推进游戏逻辑。
3.  **3v3 车轮战**:
    回合、换人、胜负全部由客户端逻辑帧 (`BattleMatch`) 确定性推进，服务器只转发帧与阵容；整场结束后双端各自上报 2006。