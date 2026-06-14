# KiHan

注：仅供学习，侵权删

## 简介

KiHan是一个基于Unity的火影手游复刻的学习项目，期望通过项目学习游戏开发的各个部分，从美术表现，网络同步，到架构设计。

暂时还在开发阶段，目前完成了登录，大厅，联机匹配。

[demo演示](https://www.bilibili.com/video/BV1HzEo6mERA/)

## 技术特性

客户端：

- 格斗框架：还原手游物理和逻辑
- 网络联机：采用原手游一样的帧同步方案，支持单机和网络匹配联机
- 多网络协议架构：混合使用http，WebSocket，kcp，满足不同场景的网络需求
- 角色扩展：通过继承角色接口，即可独立开发每个角色
- 角色动画帧编辑器：参照mugen的编辑器，加快人物开发

服务端：[仓库链接](https://github.com/AwayC/KiHanServer-Distributed)

- 分布式架构：分离各项业务，分布式开发
- 网关内外隔离：对外使用统一网关维护多种协议连接，隔离内部和外部网络，解耦业务与网络连接
- 语言使用：网关和登录服基于 go，业务服使用 C++， 兼顾性能和开发效率
- rpc：内部使用 grpc ，外部连接使用 protoBuf ，提高开发效率

## 下载

由于要保存美术资源，使用git的lfs(large file storage)，可以参考下面步骤保证正确下载项目，直接下载zip或者clone会导致文件不全。
```bash
# 确保下载 lfs 并激活
git lfs install

# 克隆项目
git clone http://github.com/Awayc/KiHan

# 确保拉取文件完整
git lfs pull
```
## 启动

当前引入了调试配置文件，可以配置启动时是否直接加载单机对局
在 Assets/AssetPackages/Resources/ 下右键选择 KiHan/Create Debug Config，创建 LocalDebugConfig 文件。启动时会在此路径下加载这个文件，如果有配置，就会按照配置启动项目，可在编辑器中编辑设置。
目前可选项:
```C#
[Header("--- 训练场快速调试 ---")]
public bool isBattleDebug = false; // 为true，跳过所有页面，直接加载单机对局

[Header("--- 选人测试 ---")]
public int debugPlayer1CharId = 90001; // 1P 默认鸣人
public int debugPlayer2CharId = 90001; // 2P 默认鸣人
```

