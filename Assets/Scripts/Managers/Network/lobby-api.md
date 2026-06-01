## DB表: kihan_game_players (维护用户游戏内信息)

### 表内信息
*   **uid**: string (主键，和 user 表的 uid 绑定)
*   **nickname**: varchar(64) (唯一昵称)
*   **create_time**: datetime (创建时间)
*   **data**: json (其他游戏数据，例如 `{}`)            

## 错误码定义 (Lobby Server)
范围: -2000 ~ -2999 (遵循 login_server 风格)

| 错误码 | 常量名 | 描述 |
| :--- | :--- | :--- |
| 0 | LOBBY_ERR_OK | 成功 |
| -2000 | LOBBY_ERR_API_BAD_REQ | 请求参数错误 |
| -2001 | LOBBY_ERR_API_INTERNAL_ERROR | 服务器内部错误 |
| -2200 | LOBBY_ERR_API_DB_ERROR | 数据库操作失败 |
| -2201 | LOBBY_ERR_PLAYER_EXISTS | 玩家已存在 (如昵称重名) |
| -2202 | LOBBY_ERR_PLAYER_NOT_EXISTS | 玩家不存在 |
| -2300 | LOBBY_ERR_MATCH_FAILED | 匹配失败 |

## 接口协议 (Protobuf)
网关支持 WebSocket 和 KCP/UDP 两种通道。**所有业务包体格式依据通道不同有所区分**。

*   **WebSocket 通道 (常用)**: 依赖自身消息边界，无需长度前缀。格式为：`[CmdID(2 bytes)][Protobuf字节流]`
*   **KCP 流通道**: 属于字节流，需要解决粘包。格式为：`[包长(2 bytes)][CmdID(2 bytes)][Protobuf字节流]` (包长 = 2 + Protobuf字节流长度)

以下是 Payload 的 Protobuf 定义和 CmdID 映射。

---

### 通用数据结构
```protobuf
message PlayerInfo {
    string uid = 1;
    string nickname = 2;
    string data_json = 3; // JSON 格式的游戏存档数据
}
```

---

### CmdID: 1001 - 登录大厅
*   **请求: LoginReq**
    *   描述：客户端连接网关后，发送登录大厅请求。
    *   Payload：`message LoginReq {}` (空)
*   **响应: LoginRsp**
    *   描述：大厅登录响应。
    *   Payload：`message LoginRsp { int32 err_code = 1; PlayerInfo player = 2; }`

### CmdID: 1002 - 登出大厅
*   **请求: LogoutReq**
    *   描述：主动登出大厅请求。
    *   Payload：`message LogoutReq {}` (空)
*   **响应: LogoutRsp**
    *   描述：登出大厅响应。
    *   Payload：`message LogoutRsp { int32 err_code = 1; }`

### CmdID: 1003 - 创建角色
*   **请求: CreateRoleReq**
    *   描述：创建角色时客户端发出的请求。
    *   Payload：`message CreateRoleReq { string nickname = 1; }`
*   **响应: CreateRoleRsp**
    *   描述：创建角色响应。
    *   Payload：`message CreateRoleRsp { int32 err_code = 1; }`

### CmdID: 1004 - 通知客户端创建角色
*   **请求: 无**
    *   描述：Server 主动下发。当 `LoginReq` 发现玩家在表中没有角色时，下发此通知让客户端弹创建角色界面。
*   **响应: CreateRoleNtf**
    *   Payload：`message CreateRoleNtf {}` (空)

### CmdID: 1005 - 匹配游戏
*   **请求: MatchGameReq**
    *   描述：请求开始匹配。
    *   Payload：`message MatchGameReq { int32 character_id = 1; }`
*   **响应: MatchGameRsp**
    *   描述：收到匹配请求的确认（并不代表匹配成功，仅代表进入队列）。
    *   Payload：`message MatchGameRsp { int32 err_code = 1; }`

### CmdID: 1006 - 取消匹配
*   **请求: MatchStopReq**
    *   描述：请求取消匹配。
    *   Payload：`message MatchStopReq {}` (空)
*   **响应: MatchStopRsp**
    *   描述：取消匹配的响应。
    *   Payload：`message MatchStopRsp { int32 err_code = 1; bool success = 2; }`

### CmdID: 1007 - 匹配成功通知
*   **请求: 无**
    *   描述：Server 主动下发。匹配成功，房间已经通过 `game_server` 创建完毕。
*   **响应: MatchGameNtf**
    *   Payload：
        ```protobuf
        message MatchGameNtf { 
            int32 err_code = 1; 
            string room_id = 2;
            int32 position = 3;
            string room_snapshot_json = 4; // 例如 "{1p_info: {charactor_id, nickname}, 2p_info: ...}"
        }
        ```

### CmdID: 1008 - 获取玩家数据 (新增)
*   **请求: GetPlayerDataReq**
    *   描述：客户端主动拉取玩家最新数据。
    *   Payload：`message GetPlayerDataReq {}`
*   **响应: GetPlayerDataRsp**
    *   描述：返回玩家详细信息。
    *   Payload：`message GetPlayerDataRsp { int32 err_code = 1; PlayerInfo player = 2; }`

### CmdID: 1009 - 获取在线人数
*   **请求: GetOnlineCountReq**
    *   描述：获取当前大厅在线的总玩家人数。
    *   Payload：`message GetOnlineCountReq {}` (空)
*   **响应: GetOnlineCountRsp**
    *   描述：在线人数响应。
    *   Payload：`message GetOnlineCountRsp { int32 err_code = 1; int32 online_count = 2; }`