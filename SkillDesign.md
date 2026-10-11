### 霸体等级
1. 无霸体
2. 技能体
3. 霸体
4. 金刚体
6. 无敌

### 攻击种类
1. 普通攻击
2. 技能攻击
3. 抓取
4. 毒伤


### 动作类型
冲刺
跳跃
落地
滞空
闪现

### 状态类型
#### 非受击
ground : 
    idle
    move
    skill
    land
    standup
    替身无敌

air : 
    fall
    move
    skill



-----

actionInfo: 
- id
- animKey: 动画名
- catalog: 类型标签
- cancelTag[]: 取消其他动作的标签
    
- BeCancelledTag: 可以被取消的标签
- TempBeCancelledTag 
- command: 动作命令
- inputAcceptance: 动作下，玩家允许的主动移动的倍率
- autoNextActionId: 自然下一个动作
- keepPlayingAnim: 是否保持播放，防止被自己cancel打断动画
- autoTerminate: 是否自动终止，如蓄力，移动
- attacks
- attackPhase: 攻击盒开启阶段
- defencePhase: 受击开启阶段
- rootMotion: 位移分段列表 (frameRange + moveX/moveY/tweenMethod 水平缓动位移 + velocityZ/accelerationZ 竖直速度模型)
- priority: 优先级  


