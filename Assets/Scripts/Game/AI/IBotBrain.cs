namespace KiHan.Logic.AI
{
    /// <summary>
    /// 人机大脑：每个逻辑帧为一名角色生成一帧输入 (只读当前逻辑状态，只写 output，不修改世界)
    ///
    /// 人机等同于一名"看着画面按键"的玩家：输入在生成该槽位的输入帧时产生，随帧进入 BattleWorld，
    /// 与真人输入走同一条路径 (按下 / 松开语义、打断、CD 全部一致)。
    /// 单机由虚拟网络在打包输入帧时调用 (训练场的"敌人攻击")；接入正式人机时实现本接口 (行为树、脚本、其他决策方式均可)，
    /// 联机人机可在服务端或代打客户端以同样方式生成输入，不需要改动逻辑层
    /// </summary>
    public interface IBotBrain
    {
        /// <summary>
        /// 开始 / 重新开始控制 (清空内部计时与记忆)
        /// </summary>
        void Reset();

        /// <summary>
        /// 生成本帧输入
        /// </summary>
        /// <param name="self">被控制的角色</param>
        /// <param name="target">当前目标 (可为 null)</param>
        /// <param name="frameId">即将执行的逻辑帧号 (用于确定性的节奏与随机)</param>
        /// <param name="output">输出：按住的按键与摇杆方向 (调用方复用同一实例)</param>
        void Think(CharacterObj self, CharacterObj target, uint frameId, InputFrame output);
    }
}
