# KiHan 网关客户端接入文档

本文档描述了客户端如何与 KiHan Gateway（网关）建立连接、完成认证，以及收发业务数据的协议格式。

网关目前支持 **WebSocket**（主要用于大厅、聊天等可靠通讯）以及 **UDP/KCP**（用于战斗等低延迟通讯）。**所有业务通讯必须先建立 WebSocket 连接完成认证。**

## 1. WebSocket 建立与认证

客户端首选通过 WebSocket 连接网关，并通过 URL 参数传递 Token 完成鉴权。

*   **连接地址**: `ws://<Gateway_IP>:<Gateway_Port>/ws?token=<Your_Token>`
*   **协议类型**: 必须使用二进制格式（`binaryType = "arraybuffer"`）

### 1.1 认证响应 (Auth Response)
连接成功建立的瞬间，网关会下发一条 **JSON 格式的文本消息** 作为鉴权结果。

**成功响应示例:**
```json
{
    "code": 0,
    "msg": "Success",
    "conn_id": 12345,   // 你的连接标识 ID (后续 UDP 握手必须用到)
    "key": 67890,       // 你的安全密钥 Key (后续 UDP 握手必须用到)
    "udp_port": 8888    // 网关监听的 UDP/KCP 端口
}
```
*   **注意**：客户端需要将 `conn_id` 和 `key` 在内存中保存下来。如果收到 `code != 0`，说明 Token 无效，连接会被网关断开。

---

## 2. WebSocket 业务通讯格式 (核心)

收到鉴权成功的 JSON 后，后续所有的收发消息都必须是 **二进制 (Binary / ArrayBuffer)**。

得益于 WebSocket 自带的消息边界，客户端**不需要处理粘包，也不需要拼接长度前缀**。

### 2.1 数据帧格式

| 偏移 (Offset) | 长度 (Length) | 数据类型 | 说明 |
| :--- | :--- | :--- | :--- |
| 0 | 2 字节 | uint16 (大端序 BigEndian) | **CmdID**: 业务命令字。`< 2000` 会路由给 Lobby，`>= 2000` 路由给 Game。 |
| 2 | 变长 | bytes | **Payload**: 具体的 Protobuf 序列化二进制数据。如果没有数据则为空。 |

### 2.2 JavaScript / TypeScript 示例代码

```javascript
const token = "my_test_token";
const ws = new WebSocket(`ws://127.0.0.1:8080/ws?token=${token}`);
ws.binaryType = "arraybuffer"; // 必须设置为二进制

let isAuthed = false;
let connId = 0;
let connKey = 0;
let udpPort = 0;

ws.onmessage = (event) => {
    // 1. 处理首次鉴权响应 (JSON文本)
    if (!isAuthed && typeof event.data === "string") {
        const res = JSON.parse(event.data);
        if (res.code === 0) {
            isAuthed = true;
            connId = res.conn_id;
            connKey = res.key;
            udpPort = res.udp_port;
            console.log("鉴权成功！可以开始发送业务数据了。");
        } else {
            console.error("鉴权失败:", res.msg);
        }
        return;
    }

    // 2. 处理业务消息 (二进制)
    if (isAuthed && event.data instanceof ArrayBuffer) {
        const view = new DataView(event.data);
        // 读取前 2 个字节作为 CmdID (大端序)
        const cmdId = view.getUint16(0, false); 
        // 截取剩余部分作为 Protobuf 数据
        const payload = new Uint8Array(event.data, 2); 
        
        console.log(`收到业务包 CmdID: ${cmdId}, Payload长度: ${payload.length}`);
        // TODO: 将 payload 交给 protobuf 解析
    }
};

// 发送业务数据封装函数
function sendPacket(cmdId, protobufBytes) {
    if (!isAuthed) return;
    
    // 总长度 = 2字节 CmdID + protobuf数据长度
    const buffer = new ArrayBuffer(2 + protobufBytes.length);
    const view = new DataView(buffer);
    
    // 1. 写入 CmdID (大端序)
    view.setUint16(0, cmdId, false); 
    
    // 2. 写入 Protobuf 数据
    const payloadView = new Uint8Array(buffer, 2);
    payloadView.set(protobufBytes);
    
    // 3. 发送
    ws.send(buffer);
}
```

---

## 3. (进阶) KCP / UDP 通讯格式

如果游戏有高频移动、战斗等需求，可以使用网关的 UDP/KCP 通道（端口为鉴权响应中返回的 `udp_port`）。

### 3.1 UDP 混合包头 (9 字节)
所有的 UDP 包（无论是握手还是 KCP 数据），最外层必须包裹 9 字节的网关头：

| 偏移 | 长度 | 数据类型 | 说明 |
| :--- | :--- | :--- | :--- |
| 0 | 4 字节 | uint32 (大端序) | **ID**: WS 鉴权获取的 `conn_id` |
| 4 | 4 字节 | uint32 (大端序) | **Key**: WS 鉴权获取的 `key` |
| 8 | 1 字节 | uint8 | **PacketType**: `0`=UDP握手心跳, `1`=KCP流, `2`=裸UDP业务 |

### 3.2 KCP 流格式 (带长度前缀)
如果 PacketType 为 `1`（KCP），网关会把收到的数据塞进 kcp 状态机还原成字节流。**注意：与 WS 不同，KCP 属于字节流，必须有 2 字节长度前缀来解决粘包。**

发给 KCP 的业务帧格式如下：
*   **Total Length**: 2 字节 (大端序)。值 = `2 (CmdID长度) + Payload 长度`。
*   **CmdID**: 2 字节 (大端序)。
*   **Payload**: 变长 Protobuf 数据。

**KCP 封包举例：**
假设要发送 CmdID=101，数据长 5 字节，那么你塞入 kcp.Send() 的数据应该是：
`[Length=7 (2字节)][CmdID=101 (2字节)][Protobuf数据 (5字节)]`
而在最外层的物理 UDP 层面，上面这段数据还会被包裹上 9 字节的 `[ID][Key][Type=1]` 头。