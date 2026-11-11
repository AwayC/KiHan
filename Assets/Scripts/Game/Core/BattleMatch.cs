using TrueSync;

namespace KiHan.Logic
{
    /// <summary>
    /// 对局阶段 (按逻辑帧推进，联机双端一致)
    /// </summary>
    public enum MatchPhase
    {
        /// <summary>开局阵容展示 (只在第一回合前)</summary>
        Loading,
        /// <summary>回合开场播报 (第几回合 → 开始)，不能操作</summary>
        Intro,
        /// <summary>对战中，倒计时</summary>
        Fighting,
        /// <summary>回合结束播报 (时间耗尽 / 胜负 / 平局)，不能操作</summary>
        RoundEnd,
        /// <summary>换人 (下一回合出战信息)，不能操作；SwapAtTick 时执行换人</summary>
        Swap,
        /// <summary>比赛结束</summary>
        Finished,
    }

    public enum MatchResult
    {
        None,
        P1Win,
        P2Win,
        Draw,
    }

    public enum RoundEndReason
    {
        None,
        KO,
        TimeUp,
    }

    /// <summary>
    /// 3v3 车轮战对局 (纯确定性逻辑)：每回合 1v1，胜者留场 (保留血量 / 查克拉 / CD)，负者换阵容中的下一个；平局双方都换。
    /// 一方阵容用尽时比赛结束。BattleManager 每个逻辑帧在 World.Tick 之后调用 Tick，并执行 TakeSwap 给出的换人。
    /// 各阶段时长按逻辑帧计 (由 LOGIC_FPS 推导)，界面播报按阶段与阶段内帧数显示，不自己计时。
    /// </summary>
    public sealed class BattleMatch
    {
        public static readonly int LoadingTicks = GameConfig.LOGIC_FPS * 3;

        /// <summary>开场：回合播报 → 开战播报 (StartAnnounceTicks 帧后解除锁定开始对战)</summary>
        public static readonly int IntroTicks = GameConfig.LOGIC_FPS * 3;

        /// <summary>开战播报出现到开始对战的帧数 (Intro 最后这么多帧)</summary>
        public static readonly int StartAnnounceTicks = GameConfig.LOGIC_FPS;

        public static readonly int RoundTicks = GameConfig.LOGIC_FPS * 60;

        /// <summary>KO 回合结束：倒地 → 胜负播报 (死亡角色一直躺着)</summary>
        public static readonly int RoundEndKoTicks = GameConfig.LOGIC_FPS * 4;

        /// <summary>时间到回合结束：时间耗尽播报 → 胜负播报</summary>
        public static readonly int RoundEndTimeUpTicks = GameConfig.LOGIC_FPS * 5;

        public static readonly int SwapTicks = GameConfig.LOGIC_FPS * 3;

        /// <summary>换人时机：Swap 阶段第几帧 (界面在此之前用转场盖住画面)</summary>
        public static readonly int SwapAtTick = GameConfig.LOGIC_FPS / 2;

        public BattleMatch(int lineupSize)
        {
            LineupSize = lineupSize;
            Phase = MatchPhase.Loading;
            RemainingTicks = RoundTicks;
        }

        /// <summary>每方阵容人数</summary>
        public int LineupSize { get; }

        public MatchPhase Phase { get; private set; }

        /// <summary>当前阶段已经过的逻辑帧数</summary>
        public int PhaseTick { get; private set; }

        /// <summary>当前回合 (从 1 起)</summary>
        public int Round { get; private set; } = 1;

        /// <summary>本回合剩余逻辑帧</summary>
        public int RemainingTicks { get; private set; }

        /// <summary>本回合剩余秒数 (向上取整，界面显示用)</summary>
        public int RemainingSeconds => (RemainingTicks + GameConfig.LOGIC_FPS - 1) / GameConfig.LOGIC_FPS;

        /// <summary>双方当前出战角色在阵容中的下标 (从 0 起；等于 LineupSize 表示阵容用尽)</summary>
        public int P1Index { get; private set; }
        public int P2Index { get; private set; }

        /// <summary>最近结束的回合结果与原因 (RoundEnd 之后保持到下一回合结束)</summary>
        public MatchResult RoundResult { get; private set; }
        public RoundEndReason EndReason { get; private set; }

        /// <summary>比赛结果 (Finished 之后有效)</summary>
        public MatchResult Result { get; private set; }

        /// <summary>非对战阶段不接受输入</summary>
        public bool InputLocked => Phase != MatchPhase.Fighting;

        private bool _swapPending;
        private bool _swapP1;
        private bool _swapP2;

        /// <summary>
        /// 完胜：该方获胜且一个角色都没出局
        /// </summary>
        public bool IsPerfectWin(int slot)
        {
            if (Phase != MatchPhase.Finished) return false;
            return slot == 1 ? Result == MatchResult.P1Win && P1Index == 0 : Result == MatchResult.P2Win && P2Index == 0;
        }

        /// <summary>
        /// 每个逻辑帧在 World.Tick 之后调用；p1 / p2 为双方当前出战角色
        /// </summary>
        public void Tick(CharacterObj p1, CharacterObj p2)
        {
            PhaseTick++;
            switch (Phase)
            {
                case MatchPhase.Loading:
                    if (PhaseTick >= LoadingTicks) Enter(MatchPhase.Intro);
                    break;

                case MatchPhase.Intro:
                    if (PhaseTick >= IntroTicks)
                    {
                        RemainingTicks = RoundTicks;
                        Enter(MatchPhase.Fighting);
                    }
                    break;

                case MatchPhase.Fighting:
                    TickFighting(p1, p2);
                    break;

                case MatchPhase.RoundEnd:
                    if (PhaseTick >= (EndReason == RoundEndReason.TimeUp ? RoundEndTimeUpTicks : RoundEndKoTicks)) FinishRound();
                    break;

                case MatchPhase.Swap:
                    if (PhaseTick == SwapAtTick)
                    {
                        _swapPending = true;
                        _swapP1 = RoundResult != MatchResult.P1Win;
                        _swapP2 = RoundResult != MatchResult.P2Win;
                    }
                    if (PhaseTick >= SwapTicks) Enter(MatchPhase.Intro);
                    break;
            }
        }

        /// <summary>
        /// 取走本帧的换人请求 (Swap 阶段 SwapAtTick 帧给出一次)：p1 / p2 为是否换下该方 (换成 P1Index / P2Index 对应的阵容角色)；
        /// 未换下的一方 (上回合胜者) 保留状态，由调用方回到出生点
        /// </summary>
        public bool TakeSwap(out bool p1, out bool p2)
        {
            p1 = _swapP1;
            p2 = _swapP2;
            if (!_swapPending) return false;
            _swapPending = false;
            return true;
        }

        private void TickFighting(CharacterObj p1, CharacterObj p2)
        {
            if (RemainingTicks > 0) RemainingTicks--;

            bool p1Dead = p1 == null || p1.IsDead;
            bool p2Dead = p2 == null || p2.IsDead;
            if (p1Dead || p2Dead)
            {
                EndRound(p1Dead && p2Dead ? MatchResult.Draw : p1Dead ? MatchResult.P2Win : MatchResult.P1Win, RoundEndReason.KO);
                return;
            }
            if (RemainingTicks > 0) return;

            FP hp1 = p1.Hp;
            FP hp2 = p2.Hp;
            EndRound(hp1 == hp2 ? MatchResult.Draw : hp1 > hp2 ? MatchResult.P1Win : MatchResult.P2Win, RoundEndReason.TimeUp);
        }

        private void EndRound(MatchResult result, RoundEndReason reason)
        {
            RoundResult = result;
            EndReason = reason;
            Enter(MatchPhase.RoundEnd);
        }

        /// <summary>
        /// 回合播报结束：负者 (平局双方) 出局，一方阵容用尽则比赛结束，否则进入换人
        /// </summary>
        private void FinishRound()
        {
            if (RoundResult != MatchResult.P1Win) P1Index++;
            if (RoundResult != MatchResult.P2Win) P2Index++;

            bool p1Out = P1Index >= LineupSize;
            bool p2Out = P2Index >= LineupSize;
            if (p1Out || p2Out)
            {
                Result = p1Out && p2Out ? MatchResult.Draw : p1Out ? MatchResult.P2Win : MatchResult.P1Win;
                Enter(MatchPhase.Finished);
                return;
            }

            Round++;
            Enter(MatchPhase.Swap);
        }

        private void Enter(MatchPhase phase)
        {
            Phase = phase;
            PhaseTick = 0;
        }
    }
}
