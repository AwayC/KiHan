using System;
using System.Collections.Generic;
using KiHan.Logic;
using KiHan.Math;
using TrueSync;

/// <summary>
/// 3v3 车轮战对局 (BattleMatch)：阶段时长、KO / 同时倒下、时间到比血、换人、比赛结束与完胜、留场角色回出生点
/// </summary>
static class Program
{
    static int _fail;

    static void Check(bool ok, string msg)
    {
        Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {msg}");
        if (!ok) _fail++;
    }

    static AnimationFrameData Anim(int frames)
    {
        var a = new AnimationFrameData();
        for (int i = 0; i < frames; i++) a.Steps.Add(new AnimationFrameData.AnimStep { FrameIndex = 0, Duration = 1 });
        return a;
    }

    static CharacterObj Make(int slot)
    {
        var anim = new FrameAnimator();
        anim.RegisterAnim("idle", Anim(6));
        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = slot, TeamId = slot, InputSlot = slot };
        ac.SetAllActions(new List<ActionInfo> { new ActionInfo { id = "idle", animKey = "idle", autoNextActionId = "idle" } }, "idle");
        return cha;
    }

    sealed class Arena
    {
        public readonly BattleWorld World = new BattleWorld(new MapLogic());
        public readonly BattleMatch Match = new BattleMatch(3);
        public CharacterObj P1 = Make(1);
        public CharacterObj P2 = Make(2);
        public int SwapCount;
        public bool LastSwapP1;
        public bool LastSwapP2;

        public Arena()
        {
            World.AddCharacter(P1);
            World.AddCharacter(P2);
        }

        public void Step()
        {
            World.InputLocked = Match.InputLocked;
            World.Tick(null);
            Match.Tick(P1, P2);
            if (!Match.TakeSwap(out bool s1, out bool s2)) return;
            SwapCount++;
            LastSwapP1 = s1;
            LastSwapP2 = s2;
            // 被换下的一方换成新角色 (满血)，留场方保留状态
            if (s1) { World.RemoveCharacter(P1); P1 = Make(1); World.AddCharacter(P1); }
            if (s2) { World.RemoveCharacter(P2); P2 = Make(2); World.AddCharacter(P2); }
        }

        public void Steps(int n)
        {
            for (int i = 0; i < n; i++) Step();
        }

        /// <summary>推进到对战阶段</summary>
        public void ToFighting()
        {
            int guard = 0;
            while (Match.Phase != MatchPhase.Fighting && guard++ < 100000) Step();
        }

        /// <summary>推进到指定阶段 (最多 limit 帧)，返回用了多少帧</summary>
        public int Until(MatchPhase phase, int limit = 100000)
        {
            int n = 0;
            while (Match.Phase != phase && n < limit) { Step(); n++; }
            return n;
        }
    }

    static void Kill(CharacterObj cha) => cha.TakeDamage(cha.Hp);

    static int Main()
    {
        int fps = GameConfig.LOGIC_FPS;

        Console.WriteLine("== 场景1: 开局阶段时长与输入锁定 ==");
        {
            var a = new Arena();
            Check(a.Match.Phase == MatchPhase.Loading && a.Match.InputLocked, $"开局为 Loading 且锁定输入 (phase={a.Match.Phase})");
            int loading = a.Until(MatchPhase.Intro);
            Check(loading == BattleMatch.LoadingTicks, $"Loading 持续 {loading} 帧，预期 LOGIC_FPS×3={BattleMatch.LoadingTicks}");
            int intro = a.Until(MatchPhase.Fighting);
            Check(intro == BattleMatch.IntroTicks, $"Intro 持续 {intro} 帧，预期 LOGIC_FPS×3={BattleMatch.IntroTicks}");
            Check(!a.Match.InputLocked, "对战阶段解除输入锁定");
            Check(a.Match.RemainingSeconds == 60, $"对战开始剩余 {a.Match.RemainingSeconds} 秒，预期 60");
            a.Step();
            Check(a.Match.RemainingSeconds == 60, $"过 1 帧剩余秒数向上取整仍为 {a.Match.RemainingSeconds}，预期 60");
            a.Steps(fps);
            Check(a.Match.RemainingSeconds == 59, $"再过 LOGIC_FPS 帧剩余 {a.Match.RemainingSeconds} 秒，预期 59");
        }

        Console.WriteLine("== 场景2: KO 后负者换人、胜者留场保留血量 ==");
        {
            var a = new Arena();
            a.ToFighting();
            a.P1.TakeDamage(FP.Parse("300"));
            CharacterObj winner = a.P1;
            Kill(a.P2);
            a.Step();
            Check(a.Match.Phase == MatchPhase.RoundEnd && a.Match.RoundResult == MatchResult.P1Win && a.Match.EndReason == RoundEndReason.KO,
                $"P2 倒下当帧进入 RoundEnd (phase={a.Match.Phase} result={a.Match.RoundResult} reason={a.Match.EndReason})");
            Check(a.Match.InputLocked, "回合结束锁定输入");
            int roundEnd = a.Until(MatchPhase.Swap);
            Check(roundEnd == BattleMatch.RoundEndKoTicks, $"KO 播报 {roundEnd} 帧，预期 LOGIC_FPS×4={BattleMatch.RoundEndKoTicks}");
            Check(a.Match.Round == 2 && a.Match.P1Index == 0 && a.Match.P2Index == 1, $"进入第 {a.Match.Round} 回合，P1Index={a.Match.P1Index} P2Index={a.Match.P2Index}，预期 2/0/1");
            a.Steps(BattleMatch.SwapAtTick);
            Check(a.SwapCount == 1 && !a.LastSwapP1 && a.LastSwapP2, $"Swap 第 {BattleMatch.SwapAtTick} 帧只换 P2 (次数={a.SwapCount} p1={a.LastSwapP1} p2={a.LastSwapP2})");
            Check(a.P1 == winner && a.P1.Hp == FP.Parse("700"), $"胜者留场且保留血量 {a.P1.Hp.AsFloat()}，预期 1000-300=700");
            int rest = a.Until(MatchPhase.Intro);
            Check(rest == BattleMatch.SwapTicks - BattleMatch.SwapAtTick, $"换人阶段剩余 {rest} 帧后回到 Intro，预期 {BattleMatch.SwapTicks - BattleMatch.SwapAtTick}");
        }

        Console.WriteLine("== 场景3: 同一帧双方倒下为平局，双方都换人 ==");
        {
            var a = new Arena();
            a.ToFighting();
            Kill(a.P1);
            Kill(a.P2);
            a.Step();
            Check(a.Match.RoundResult == MatchResult.Draw && a.Match.EndReason == RoundEndReason.KO, $"结果 {a.Match.RoundResult}/{a.Match.EndReason}，预期 Draw/KO");
            a.Until(MatchPhase.Swap);
            Check(a.Match.P1Index == 1 && a.Match.P2Index == 1, $"双方出局 P1Index={a.Match.P1Index} P2Index={a.Match.P2Index}，预期 1/1");
            a.Steps(BattleMatch.SwapAtTick);
            Check(a.LastSwapP1 && a.LastSwapP2, $"双方都换人 (p1={a.LastSwapP1} p2={a.LastSwapP2})");
        }

        Console.WriteLine("== 场景4: 时间到按剩余血量数值判胜，相等平局 ==");
        {
            var a = new Arena();
            a.ToFighting();
            a.P2.TakeDamage(FP.Parse("1"));
            int fight = a.Until(MatchPhase.RoundEnd);
            Check(fight == BattleMatch.RoundTicks, $"对战持续 {fight} 帧，预期 LOGIC_FPS×60={BattleMatch.RoundTicks}");
            Check(a.Match.RoundResult == MatchResult.P1Win && a.Match.EndReason == RoundEndReason.TimeUp, $"P1 血多 1 点：{a.Match.RoundResult}/{a.Match.EndReason}，预期 P1Win/TimeUp");
            int roundEnd = a.Until(MatchPhase.Swap);
            Check(roundEnd == BattleMatch.RoundEndTimeUpTicks, $"时间到播报 {roundEnd} 帧，预期 LOGIC_FPS×5={BattleMatch.RoundEndTimeUpTicks}");

            var b = new Arena();
            b.ToFighting();
            b.P1.TakeDamage(FP.Parse("250"));
            b.P2.TakeDamage(FP.Parse("250"));
            b.Until(MatchPhase.RoundEnd);
            Check(b.Match.RoundResult == MatchResult.Draw && b.Match.EndReason == RoundEndReason.TimeUp, $"血量相等：{b.Match.RoundResult}/{b.Match.EndReason}，预期 Draw/TimeUp");
        }

        Console.WriteLine("== 场景5: 一方阵容用尽比赛结束；完胜判定 ==");
        {
            var a = new Arena();
            for (int round = 1; round <= 3; round++)
            {
                a.ToFighting();
                Kill(a.P2);
                a.Step();
                a.Until(round < 3 ? MatchPhase.Swap : MatchPhase.Finished);
            }
            Check(a.Match.Phase == MatchPhase.Finished && a.Match.Result == MatchResult.P1Win, $"P2 连输 3 回合：{a.Match.Phase}/{a.Match.Result}，预期 Finished/P1Win");
            Check(a.Match.IsPerfectWin(1) && !a.Match.IsPerfectWin(2), "P1 一个角色都没出局为完胜");
            Check(a.Match.InputLocked, "比赛结束锁定输入");

            var b = new Arena();
            b.ToFighting();
            Kill(b.P1);
            b.Step();
            for (int round = 2; round <= 4; round++)
            {
                b.Until(MatchPhase.Fighting);
                Kill(b.P2);
                b.Step();
            }
            b.Until(MatchPhase.Finished);
            Check(b.Match.Result == MatchResult.P1Win && !b.Match.IsPerfectWin(1), $"P1 先输一回合再连胜三回合：{b.Match.Result}，完胜={b.Match.IsPerfectWin(1)}，预期 P1Win / 非完胜");
            Check(b.Match.Round == 4, $"共 {b.Match.Round} 回合，预期 4");

            var c = new Arena();
            for (int round = 1; round <= 3; round++)
            {
                c.ToFighting();
                Kill(c.P1);
                Kill(c.P2);
                c.Step();
            }
            c.Until(MatchPhase.Finished);
            Check(c.Match.Result == MatchResult.Draw, $"三回合都平局：{c.Match.Result}，预期 Draw");
        }

        Console.WriteLine("== 场景6: ResetForRound 回出生点、解除受击，保留血量与查克拉 ==");
        {
            var a = new Arena();
            CharacterObj p = a.P1;
            p.Skills = new SkillController();
            p.Skills.Setup(Array.Empty<SkillInfo>(), new List<ActionInfo>(), 4, 3);
            p.TakeDamage(FP.Parse("400"));
            p.EnterStun(10);
            p.motion.Reset(new FPVector3(FP.Parse("5"), FP.Zero, FP.One));
            p.ResetForRound(new FPVector3(FP.Parse("-2"), FP.Zero, FP.Zero), false);
            Check(p.Hurt == HurtState.None && p.StunTicks == 0, $"解除受击 (hurt={p.Hurt} stun={p.StunTicks})");
            Check(p.motion.Position.x == FP.Parse("-2") && p.motion.Position.z == FP.Zero, $"回到出生点 x={p.motion.Position.x.AsFloat()} z={p.motion.Position.z.AsFloat()}，预期 -2/0");
            Check(!p.FacingLeft && p.action.CurrentAction.id == "idle", $"朝右且回到默认动作 ({p.action.CurrentAction.id})");
            Check(p.Hp == FP.Parse("600") && p.Skills.Chakra == 3, $"保留血量 {p.Hp.AsFloat()} 与查克拉 {p.Skills.Chakra}，预期 600 / 3");
        }

        Console.WriteLine(_fail == 0 ? "全部通过" : $"失败 {_fail} 项");
        return _fail == 0 ? 0 : 1;
    }
}
