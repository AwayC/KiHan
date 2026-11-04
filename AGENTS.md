# KiHan 开发助手须知

开始任何工作前先读：
- [.agents/rules.md](.agents/rules.md)：强制规范 (编码、帧同步时钟、定点数、编辑器 CLI 等)
- [.agents/testing.md](.agents/testing.md)：测试分层与验证清单
- [.agents/ui.md](.agents/ui.md)：UI 规范

## Unity 编辑器：有 CLI 就跑 CLI

需要编辑器参与的操作 (编译、测试、Play 模式、截图、控制台、场景 / 资源检查、执行编辑器代码) 一律先执行 `unicli check`；连接成功就只用 `unicli` 完成：

```
unicli exec Compile --json                 # 编译，返回错误 / 警告
unicli exec TestRunner.RunEditMode --json  # 编辑器测试
unicli eval '<C#>' --json                  # 在编辑器里执行代码
unicli commands                            # 全部命令
```

只有 CLI 不可用时才退回切换窗口焦点 / 读 `Editor.log` / 临时编辑器脚本，并说明原因。临时脚本里的等待必须有超时。
