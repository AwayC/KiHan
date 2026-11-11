using KiHan.Config;
using KiHan.Logic;

namespace KiHan.UI
{
    /// <summary>
    /// 战斗 HUD 打开参数 (BattleManager 加载完战斗后经 GameFlow.OnBattleLoaded 传给 Lua 的 BattleHudPanel 与 TrainingPanel)
    /// </summary>
    public sealed class BattleHudArgs
    {
        /// <summary>战斗模式 (训练场 / 人机 / 联机)</summary>
        public BattleMode Mode;

        /// <summary>槽位 1 的角色</summary>
        public CharacterObj P1;

        /// <summary>槽位 2 的角色</summary>
        public CharacterObj P2;

        /// <summary>本机玩家控制的角色 (按键显示其技能)</summary>
        public CharacterObj Local;

        /// <summary>本机玩家的出战配置 (通灵数量)</summary>
        public PlayerLoadout LocalLoadout;

        /// <summary>单机训练场 (联机为 null)；不为 null 时 GameFlow 叠加打开训练场面板 (TrainingPanel)</summary>
        public TrainingSession Training;
    }
}
