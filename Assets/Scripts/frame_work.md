层次,核心职责,实现方式
1. 网络通讯层 (KCP),负责发送/接收二进制指令包。,C# 封装 KCP 库，处理 UDP 丢包重传。
2. 帧同步管理器 (Lockstep),驱动逻辑帧前进，收集输入，处理延迟。,自定义 FixedUpdate 逻辑循环。
3. 确定性物理逻辑层 (Deterministic),计算位移、跳跃重力、受击状态。,完全手动实现，使用定点数或整数。
4. 判定系统 (Hitbox System),判定招式是否打中人、身体碰撞。,自定义矩形检测（非 Unity Physics）。
5. 动画状态机 (Frame Animator),根据逻辑帧切换 2D 序列帧图片。,手动代码控制 SpriteRenderer。
6. 表现插值层 (View Interpolation),让逻辑位移在视觉上更丝滑。,在 Update 中做平滑过渡。