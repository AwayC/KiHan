using KiHan.Config;

namespace KiHan.UI
{
    /// <summary>
    /// 选人界面 (PraperPanel) 的打开参数
    /// </summary>
    public sealed class PrepareArgs
    {
        /// <summary>人机对战 / 联机对战 (确认时 GameFlow 据此开局或提交阵容)</summary>
        public BattleMode Mode;
    }

    /// <summary>
    /// 3v3 阵容 Loading (BattleLoading3v3Panel) 的打开参数
    /// </summary>
    public sealed class BattleLoadingArgs
    {
        /// <summary>双方阵容 (按出战顺序)</summary>
        public PlayerLoadout[] P1Lineup;
        public PlayerLoadout[] P2Lineup;

        /// <summary>本机槽位 (1 / 2)</summary>
        public int LocalSlot;
    }
}
