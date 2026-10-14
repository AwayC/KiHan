using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using KiHan.Logic;
using UnityEngine;

static class Program
{
    static int _fail;
    static void Check(bool ok, string msg) { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {msg}"); if (!ok) _fail++; }

    // 按动画名逐帧配置 "grab" 锚点 (像素)，返回 null 表示该帧没有锚点
    static readonly Dictionary<string, Func<int, Vector2Int?>> Anchors = new Dictionary<string, Func<int, Vector2Int?>>();

    static AnimationFrameData Anim(int frames, bool hit, Func<int, Vector2Int?> anchor = null)
    {
        var lib = new CharacterSpriteLibrary();
        var a = new AnimationFrameData { Library = lib };
        for (int i = 0; i < frames; i++)
        {
            var fd = new SpriteFrameData();
            fd.HurtBoxes.Add(new LogicBox(new Vector2Int(0, 80), new Vector2Int(60, 160), 40));
            if (hit) fd.HitBoxes.Add(new LogicBox(new Vector2Int(60, 80), new Vector2Int(120, 160), 40));
            Vector2Int? p = anchor?.Invoke(i);
            if (p.HasValue) fd.EffectAnchors.Add(new EffectAnchorInfo { Name = "grab", Position = p.Value });
            lib.AllFrames.Add(fd);
            a.Steps.Add(new AnimationFrameData.AnimStep { FrameIndex = i, Duration = 1 });
        }
        return a;
    }

    static ActionInfo Act(string id, string next, bool loop = false) =>
        new ActionInfo { id = id, animKey = id, autoNextActionId = next, keepPlayingAnim = loop };

    static ActionInfo Attack(string id, AttackData atk, int frames, string next = "idle")
    {
        var a = Act(id, next);
        a.attackPhase = new[] { new AttackBoxTurnOnInfo { frameRanges = new[] { new FrameRange(0, frames) }, attackPhase = 0 } };
        a.attacks = new[] { atk.ToRuntime() };
        return a;
    }

    static ActionChangeData To(string id) => new ActionChangeData { changeType = 1, param = id, priority = 100 };

    // 公共受击动作集
    static List<ActionInfo> CommonActions()
    {
        var idle = Act("idle", "idle", true);
        // 通用受击动作：固定 id，只提供动画；autoNext / onLand / 霸体都不配置，流程与起身无敌由框架驱动
        var hurt1 = Act("hurt_1", "");
        var hurt2 = Act("hurt_2", "");
        var toAir = Act("hurt_toair", "");
        var inAir = Act("hurt_inair", "");
        var fall = Act("hurt_fall", "");
        var lie = Act("lie", "");
        var standup = Act("standup", "");
        var armored = Act("armored", "armored", true);
        armored.armorPhase = new[] { new ArmorPhaseInfo { range = new FrameRange(0, 99), level = ArmorLevel.Super } };
        return new List<ActionInfo> { idle, hurt1, hurt2, toAir, inAir, fall, lie, standup, armored };
    }

    static CharacterObj Make(int id, int team, string x, string start, params ActionInfo[] extra)
    {
        var anim = new FrameAnimator();
        var actions = CommonActions();
        actions.AddRange(extra);
        foreach (var a in actions)
        {
            int frames = a.id == "lie" ? 20 : a.id == "standup" ? 6 : a.id.StartsWith("hurt") ? 3 : 8;
            if (a.id == "grab" || a.id == "carry") frames = 10;
            Anchors.TryGetValue(a.animKey, out var anchorOf);
            anim.RegisterAnim(a.animKey, Anim(frames, a.attacks != null && a.attacks.Length > 0, anchorOf));
        }
        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = id, TeamId = team };
        ac.Set((p, q) => cha.HitRecords.Clear());
        ac.SetAllActions(actions, start);
        cha.motion.Reset(new FPVector3(FP.Parse(x), FP.Zero, FP.Zero));
        return cha;
    }

    // 直接击倒 (需在加入 BattleWorld 之后调用，加入时会重置战斗状态)：第一次 Tick 结束时切到 lie
    static BattleWorld WorldLying(CharacterObj a, CharacterObj t)
    {
        var w = World(a, t);
        t.EnterLying();
        return w;
    }

    static BattleWorld World(params CharacterObj[] chars)
    {
        var w = new BattleWorld(new MapLogic());
        foreach (var c in chars) w.AddCharacter(c);
        return w;
    }

    static uint _f;
    static void Step(BattleWorld w, int n = 1) { var fr = new RoomFrame(); for (int i = 0; i < n; i++) { fr.FrameId = ++_f; w.Tick(fr); } }
    static float F(FP v) => v.AsFloat();

    // 仓库根目录 (按本源文件位置推算，与运行目录无关)
    static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file), "../../.."));

    static int Main()
    {
        // 不配置 targetActionChange：僵直默认进入 hurt_1
        var normal = new AttackData { attack = "50", canHitSameTarget = 1, hitStun = "0.3" };

        Console.WriteLine("== 1. 霸体：目标 Super 霸体，普通攻击只扣血；破霸 Super 的攻击完整受击 ==");
        {
            var a = Make(1, 1, "0", "atk", Attack("atk", normal, 8));
            var t = Make(2, 2, "0.9", "armored");
            var w = World(a, t); Step(w);
            Check(w.HitEvents.Count == 1 && w.HitEvents[0].Reaction == HitReaction.ArmorBlocked, $"反应={(w.HitEvents.Count > 0 ? w.HitEvents[0].Reaction.ToString() : "无命中")}");
            Check(t.action.CurrentAction.id == "armored" && t.Hurt == HurtState.None, $"动作未被打断: {t.action.CurrentAction.id}, 状态 {t.Hurt}");
            Check(F(t.Hp) == 950f, $"仍然扣血: Hp={F(t.Hp)}");

            var breaker = normal; breaker.armorBreak = "Super";
            var a2 = Make(1, 1, "0", "atk", Attack("atk", breaker, 8));
            var t2 = Make(2, 2, "0.9", "armored");
            var w2 = World(a2, t2); Step(w2);
            Check(t2.Hurt == HurtState.Stun && t2.action.CurrentAction.id == "hurt_1", $"破霸后完整受击: {t2.Hurt}, {t2.action.CurrentAction.id}");
        }

        Console.WriteLine("== 1b. 生命值归零后不再受击；无限血目标生命值不低于 1，可持续被命中 ==");
        {
            var a = Make(1, 1, "0", "atk", Attack("atk", normal, 8));
            var t = Make(2, 2, "0.9", "idle");
            t.maxHp = FP.Parse("50");
            var w = World(a, t); Step(w);
            Check(t.IsDead && w.HitEvents.Count == 1, $"一击归零: Hp={F(t.Hp)}");
            a.action.SetAllActions(a.action.AllActions, "atk");
            Step(w);
            Check(w.HitEvents.Count == 0, "死亡目标不再产生命中");

            var a2 = Make(1, 1, "0", "atk", Attack("atk", normal, 8));
            var t2 = Make(2, 2, "0.9", "idle");
            t2.maxHp = FP.Parse("50");
            t2.infiniteHp = true;
            var w2 = World(a2, t2); Step(w2);
            Check(!t2.IsDead && F(t2.Hp) == 1f && w2.HitEvents.Count == 1, $"无限血扣到 1: Hp={F(t2.Hp)}");
            a2.action.SetAllActions(a2.action.AllActions, "atk");
            Step(w2);
            Check(w2.HitEvents.Count == 1 && F(t2.Hp) == 1f, $"仍可被命中且保持 1: 命中 {w2.HitEvents.Count}, Hp={F(t2.Hp)}");
        }

        Console.WriteLine("== 2. 无敌：armorPhase 配置 Invincible 的动作 (如闪避)，不产生命中 ==");
        {
            var a = Make(1, 1, "0", "atk", Attack("atk", normal, 8));
            var dodge = Act("dodge", "dodge", true);
            dodge.armorPhase = new[] { new ArmorPhaseInfo { range = new FrameRange(0, 99), level = ArmorLevel.Invincible } };
            var t = Make(2, 2, "0.9", "dodge", dodge);
            var w = World(a, t); Step(w, 3);
            Check(w.HitEvents.Count == 0 && F(t.Hp) == 1000f, $"无命中, Hp={F(t.Hp)}");
        }

        Console.WriteLine("== 3. 倒地/扫地：普通攻击打不到倒地目标；扫地无击飞保持倒地；扫地带击飞重新挑起 ==");
        {
            var a = Make(1, 1, "0", "atk", Attack("atk", normal, 8));
            var t = Make(2, 2, "0.9", "idle");
            var w = WorldLying(a, t); Step(w, 3);
            Check(w.HitEvents.Count == 0 && t.IsLying && t.action.CurrentAction.id == "lie", $"普通攻击未命中倒地目标: {t.Hurt}, {t.action.CurrentAction.id}");

            var otg = normal; otg.canHitLying = true;
            var a2 = Make(1, 1, "0", "atk", Attack("atk", otg, 8));
            var t2 = Make(2, 2, "0.9", "idle");
            var w2 = WorldLying(a2, t2); Step(w2);
            Check(w2.HitEvents.Count == 1 && w2.HitEvents[0].Reaction == HitReaction.Lying, $"扫地命中, 反应={(w2.HitEvents.Count > 0 ? w2.HitEvents[0].Reaction.ToString() : "无")}");
            Check(t2.action.CurrentAction.id == "lie" && t2.Hurt == HurtState.Lying && F(t2.Hp) == 950f, $"保持倒地并扣血: {t2.action.CurrentAction.id}, Hp={F(t2.Hp)}");

            var otgLaunch = otg; otgLaunch.launchVelocity = "6"; // 不配置 launchActionChange：默认 hurt_toair
            var a3 = Make(1, 1, "0", "atk", Attack("atk", otgLaunch, 8));
            var t3 = Make(2, 2, "0.9", "idle");
            var w3 = WorldLying(a3, t3); Step(w3, 2);
            Check(t3.Hurt == HurtState.Launch && t3.action.CurrentAction.id == "hurt_toair" && F(t3.motion.Position.z) > 0, $"扫地挑起: {t3.Hurt}, {t3.action.CurrentAction.id}, z={F(t3.motion.Position.z):F3}");
        }

        Console.WriteLine("== 4. 空中追击上挑：空中目标被 airLaunchVelocity=4 的攻击命中，竖直速度重置为 4 ==");
        {
            var juggle = normal; juggle.airLaunchVelocity = "4"; juggle.canHitSameTarget = 1; 
            var a = Make(1, 1, "0", "idle", Attack("atk", juggle, 8));
            var t = Make(2, 2, "0.9", "hurt_fall");
            var w = World(a, t);
            t.motion.Position = new FPVector3(FP.Parse("0.9"), FP.Zero, FP.Parse("0.5"));
            t.motion.Launch(FP.Parse("-3"));
            t.EnterLaunch();
            a.action.SetAllActions(a.action.AllActions, "atk");
            Step(w);
            Check(w.HitEvents.Count == 1 && w.HitEvents[0].Reaction == HitReaction.Launch, "空中命中 -> 击飞反应");
            Check(F(t.motion.VelocityZ) == 4f, $"竖直速度被重置为 4: vz={F(t.motion.VelocityZ):F3}");
            Check(t.action.CurrentAction.id == "hurt_inair", $"空中受击默认 hurt_inair: {t.action.CurrentAction.id}");
        }

        Console.WriteLine("== 5. 抓取 (锚点即抓取)：目标逐帧跟随帧锚点；跨动作保持；锚点消失即释放 ==");
        {
            // grab 10 帧全程有锚点 (60, 50)；carry 10 帧，第 0~5 帧锚点逐帧前移 10 像素并抬高，第 6 帧起无锚点 = 主动松手
            Anchors["grab"] = f => new Vector2Int(60, 50);
            Anchors["carry"] = f => f <= 5 ? new Vector2Int(60 + 10 * f, 50 + 10 * f) : (Vector2Int?)null;
            var grabAtk = new AttackData { attack = "20", canHitSameTarget = 1,
                grab = new GrabData { enabled = true, releaseActionChange = To("hurt_fall") } };
            var grab = Attack("grab", grabAtk, 9, "carry");
            var carry = Act("carry", "idle");
            carry.rootMotion = new[] { new RootMotionSegmentData { frameRange = new FrameRange(0, 9), moveX = "1" }.ToRuntime() };
            var a = Make(1, 1, "0", "grab", grab, carry);
            Check(Debug.ErrorCount == 0, $"锚点校验通过 ({Debug.LastError})");
            var t = Make(2, 2, "0.9", "idle");
            var w = World(a, t);
            Step(w);
            Check(t.Hurt == HurtState.Grabbed && a.GrabTarget == t && t.action.CurrentAction.id == "hurt_2", $"抓取成功, 默认受击动作: {t.Hurt}, {t.action.CurrentAction.id}");
            Step(w, 9);
            Check(a.action.CurrentAction.id == "carry" && a.GrabTarget == t, $"切到 carry 后仍保持抓取 (无需 holdGrab): {a.action.CurrentAction.id}");
            bool follow = true; string trace = "";
            for (int i = 0; i < 6; i++)
            {
                Step(w);
                int f = a.action.anim.TickToFrame - 1;
                FP ex = a.motion.Position.x + FP.FromInt(60 + 10 * f) / 100;
                FP ez = FP.FromInt(50 + 10 * f) / 100;
                if (t.motion.Position.x != ex || t.motion.Position.z != ez) follow = false;
                trace += $" f{f}:({F(t.motion.Position.x):F2},{F(t.motion.Position.z):F2})";
            }
            Check(follow && a.GrabTarget == t, $"逐帧跟随锚点:{trace}");
            Step(w);
            Check(a.GrabTarget == null && t.GrabbedBy == null && t.Hurt == HurtState.Launch && t.action.CurrentAction.id == "hurt_fall",
                $"carry 第 6 帧锚点消失 -> 释放, 空中转击飞: {t.Hurt}, {t.action.CurrentAction.id}");
            Step(w, 40);
            Check(F(t.motion.Position.z) == 0f && t.Hurt != HurtState.Launch, $"落地 (弹起后倒地/起身): {t.Hurt}, {t.action.CurrentAction.id}");
        }

        Console.WriteLine("== 5b. 抓取被打断：第三方命中抓取者，受击动作无锚点 -> 释放，目标按 releaseActionChange 倒地 ==");
        {
            Anchors["grab"] = f => new Vector2Int(60, 0);
            Anchors["carry"] = f => new Vector2Int(60, 0);
            var grabAtk = new AttackData { attack = "20", canHitSameTarget = 1,
                grab = new GrabData { enabled = true, releaseActionChange = To("lie") } };
            var grab = Attack("grab", grabAtk, 9, "carry");
            var carry = Act("carry", "carry", true);
            var a = Make(1, 1, "0", "grab", grab, carry);
            var t = Make(2, 2, "0.9", "idle");
            var third = Make(3, 3, "-0.9", "idle", Attack("atk", normal, 8));
            var w = World(a, t, third);
            Step(w, 3);
            Check(t.Hurt == HurtState.Grabbed, "抓取中");
            var fr0 = new RoomFrame();
            long gc0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) { fr0.FrameId = ++_f; w.Tick(fr0); }
            long gc1 = GC.GetAllocatedBytesForCurrentThread() - gc0;
            Check(gc1 == 0 && t.Hurt == HurtState.Grabbed, $"保持抓取 300 帧 (含锚点逐帧读取) 托管分配 {gc1} bytes");
            third.action.SetAllActions(third.action.AllActions, "atk");
            Step(w);
            Check(a.Hurt == HurtState.Stun, $"抓取者被第三方打出僵直: {a.Hurt}");
            Step(w);
            Check(a.GrabTarget == null && t.Hurt == HurtState.None && t.action.CurrentAction.id == "lie", $"抓取解除, 目标倒地: {t.Hurt}, {t.action.CurrentAction.id}");
        }

        Console.WriteLine("== 5d. 被抓中受击：第三方命中只扣血、保持抓取；其他抓取无法覆盖；锚点消失同帧的投掷攻击正常击飞 ==");
        {
            // carry：第 0~5 帧有锚点，第 6 帧锚点消失并开启投掷攻击盒 (第 6~7 帧)
            Anchors["grab"] = f => new Vector2Int(60, 50);
            Anchors["carry"] = f => f <= 5 ? new Vector2Int(60, 50) : (Vector2Int?)null;
            Anchors["dgrab"] = f => new Vector2Int(60, 50);
            var grabAtk = new AttackData { attack = "20", canHitSameTarget = 1, grab = new GrabData { enabled = true } };
            var grab = Attack("grab", grabAtk, 9, "carry");
            var throwAtk = new AttackData { attack = "50", canHitSameTarget = 1, launchVelocity = "8" };
            var carry = Attack("carry", throwAtk, 9, "idle");
            carry.attackPhase = new[] { new AttackBoxTurnOnInfo { frameRanges = new[] { new FrameRange(6, 7) }, attackPhase = 0 } };
            var a = Make(1, 1, "0", "grab", grab, carry);
            var t = Make(2, 2, "0.9", "idle");
            var c = Make(3, 1, "0", "idle", Attack("atk", normal, 8));
            var d = Make(4, 1, "0", "idle", Attack("dgrab", grabAtk, 9, "idle"));
            var w = World(a, t, c, d);
            Step(w);
            Check(t.Hurt == HurtState.Grabbed && t.GrabbedBy == a, $"抓取成功: {t.Hurt}");

            c.action.SetAllActions(c.action.AllActions, "atk");
            float hp0 = F(t.Hp);
            Step(w);
            bool grabbedHit = w.HitEvents.Exists(e => e.TargetId == 2 && e.Reaction == HitReaction.GrabbedHit);
            Check(grabbedHit && t.Hurt == HurtState.Grabbed && t.GrabbedBy == a && F(t.Hp) == hp0 - 50f && t.action.CurrentAction.id == "hurt_2",
                $"第三方命中只扣血: 反应 GrabbedHit={grabbedHit}, {t.Hurt}, Hp {hp0}->{F(t.Hp)}, 动作 {t.action.CurrentAction.id}");

            d.action.SetAllActions(d.action.AllActions, "dgrab");
            Step(w);
            Check(t.GrabbedBy == a && d.GrabTarget == null && a.GrabTarget == t, $"其他抓取无法覆盖: 抓取者={(t.GrabbedBy == a ? "a" : "其他")}, d.GrabTarget={(d.GrabTarget == null ? "null" : "t")}");

            bool thrown = false; string info = "";
            for (int i = 0; i < 30 && !thrown; i++)
            {
                Step(w);
                if (a.GrabTarget == null)
                {
                    thrown = true;
                    bool launchHit = w.HitEvents.Exists(e => e.TargetId == 2 && e.AttackerId == 1 && e.Reaction == HitReaction.Launch);
                    info = $"carry 帧 {a.action.anim.TickToFrame - 1}, 投掷命中={launchHit}, {t.Hurt}, vz={F(t.motion.VelocityZ):F2}, 动作 {t.action.CurrentAction.id}";
                    thrown = launchHit && t.Hurt == HurtState.Launch && F(t.motion.VelocityZ) > 0;
                    break;
                }
            }
            Check(thrown, $"锚点消失同帧投掷击飞: {info}");
            Anchors.Clear();
        }

        Console.WriteLine("== 5c. 锚点校验：抓取段开启区间内缺锚点时报错 ==");
        {
            Anchors["grab"] = f => f < 3 ? new Vector2Int(60, 0) : (Vector2Int?)null;
            int before = Debug.ErrorCount;
            var grabAtk = new AttackData { attack = "20", canHitSameTarget = 1, grab = new GrabData { enabled = true } };
            Make(1, 1, "0", "idle", Attack("grab", grabAtk, 5, "idle"));
            Check(Debug.ErrorCount == before + 1, $"报错: {Debug.LastError}");
            Anchors.Clear();
        }

        Console.WriteLine("== 6. 吸附：目标在 2.5，吸附核心点为攻击者前方 1.0，速度 3/s，持续 0.5s ==");
        {
            var suck = normal; suck.suction = new SuctionData { centerX = "1.0", speed = "3", inSec = "0.5" };
            var a = Make(1, 1, "0", "atk", Attack("atk", suck, 8));
            var t = Make(2, 2, "0.9", "idle");
            var w = World(a, t);
            Step(w);
            t.motion.Position = new FPVector3(FP.Parse("2.5"), FP.Zero, FP.Zero);
            Step(w, 10);
            Check(F(t.motion.Position.x) < 2.5f && F(t.motion.Position.x) >= 1.0f, $"10 帧后被拉近: x={F(t.motion.Position.x):F3} (预期 2.5-3×10/30=1.5)");
            Step(w, 20);
            Check(F(t.motion.Position.x) == 1.0f, $"不越过核心点: x={F(t.motion.Position.x):F3}");
        }

        Console.WriteLine("== 7. 隐性姿态：空中动作落地兜底；带起跳的动作起跳前不触发 ==");
        {
            // 配置 onLand 且不起跳 = 空中动作：在地面上进入视同落地
            var airAtk = Act("air_atk", "air_atk", true);
            airAtk.onLand = new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "standup", priority = 100 };
            Check(airAtk.IsAirAction, "onLand + 无起跳 => 空中动作");
            var c = Make(1, 1, "0", "air_atk", airAtk);
            var w = World(c); Step(w);
            Check(c.action.CurrentAction.id == "standup", $"地面上的空中动作兜底触发 onLand: {c.action.CurrentAction.id}");

            // 配置 onLand 且起跳 (第 3 帧) = 地面起跳动作：起跳前不触发，起跳后落地才触发
            var hop = Act("hop", "hop", true);
            hop.onLand = airAtk.onLand;
            hop.rootMotion = new[] { new RootMotionSegmentData { frameRange = new FrameRange(3, 3), velocityZ = "5" }.ToRuntime() };
            Check(!hop.IsAirAction, "onLand + 起跳 => 非空中动作");
            var c2 = Make(1, 1, "0", "hop", hop);
            var w2 = World(c2); Step(w2, 3);
            Check(c2.action.CurrentAction.id == "hop", $"起跳前在地面不触发 onLand: {c2.action.CurrentAction.id}");
            int landTick = -1;
            for (int i = 0; i < 30 && landTick < 0; i++) { Step(w2); if (c2.action.CurrentAction.id == "standup") landTick = i; }
            Check(landTick > 5, $"起跳后落地触发 onLand (第 {landTick} 帧)");
        }

        Console.WriteLine("== 8. hitStun 为 0 且未配置 targetActionChange：不僵直也不切受击动作 ==");
        {
            var poke = normal; poke.hitStun = "";
            var a = Make(1, 1, "0", "atk", Attack("atk", poke, 8));
            var t = Make(2, 2, "0.9", "idle");
            var w = World(a, t); Step(w);
            Check(w.HitEvents.Count == 1 && t.Hurt == HurtState.None && t.action.CurrentAction.id == "idle", $"{t.Hurt}, {t.action.CurrentAction.id}");
        }

        Console.WriteLine("== 9. 通用受击动作缺失检查 ==");
        {
            var missing = HurtActionIds.FindMissing(new List<ActionInfo> { Act("idle", "idle"), Act("hurt_1", "idle") });
            Check(missing.Count == 6 && !missing.Contains("hurt_1"), $"缺失: {string.Join(",", missing)}");
            Check(HurtActionIds.FindMissing(CommonActions()).Count == 0, "公共动作集完整");
        }

        Console.WriteLine("== 10. 多段 RootMotion：前冲两段重叠相加、停顿、后撤 (Slowly)、二段跳、朝向镜像 ==");
        {
            ActionInfo Multi()
            {
                var m = Act("multi", "idle");
                m.rootMotion = new[]
                {
                    new RootMotionSegmentData { frameRange = new FrameRange(0, 3), moveX = "0.6" }.ToRuntime(),
                    new RootMotionSegmentData { frameRange = new FrameRange(0, 3), moveX = "0.2", moveY = "0.1" }.ToRuntime(),
                    new RootMotionSegmentData { frameRange = new FrameRange(5, 7), moveX = "-0.3", tweenMethod = "Slowly" }.ToRuntime(),
                    new RootMotionSegmentData { frameRange = new FrameRange(0, 0), velocityZ = "4" }.ToRuntime(),
                    new RootMotionSegmentData { frameRange = new FrameRange(5, 5), velocityZ = "4" }.ToRuntime(),
                };
                return m;
            }
            var c = Make(1, 1, "0", "multi", Multi());
            var w = World(c);
            var xs = new List<float>(); var zs = new List<float>();
            for (int i = 0; i < 8; i++) { Step(w); xs.Add(F(c.motion.Position.x)); zs.Add(F(c.motion.Position.z)); }
            string tx = string.Join(" ", xs.ConvertAll(v => v.ToString("F3")));
            Check(System.Math.Abs(xs[3] - 0.8f) < 1e-4 && xs[4] == xs[3], $"前 4 帧两段相加前冲 0.8，第 4 帧停顿: {tx}");
            Check(System.Math.Abs(xs[7] - 0.5f) < 1e-4 && F(c.motion.Position.y) > 0.0999f && F(c.motion.Position.y) < 0.1001f, $"后撤 0.3 后 x=0.5, y=0.1: x={xs[7]:F3}, y={F(c.motion.Position.y):F3}");
            Check(System.Math.Abs(xs[5] - xs[4]) > System.Math.Abs(xs[7] - xs[6]) && xs[5] < xs[4], $"Slowly 后撤先快后慢: Δ5={xs[5] - xs[4]:F3}, Δ7={xs[7] - xs[6]:F3}");
            string tz = string.Join(" ", zs.ConvertAll(v => v.ToString("F3")));
            Check(zs[4] < zs[3] && zs[5] > zs[4] && zs[5] > 0, $"下落中第 5 帧二段跳重新上升: {tz}");

            var c2 = Make(1, 1, "0", "multi", Multi());
            c2.FacingLeft = true; // 无输入组件也能转向
            var w2 = World(c2);
            Step(w2, 8);
            Check(System.Math.Abs(F(c2.motion.Position.x) + 0.5f) < 1e-4, $"面朝左时 x 镜像: x={F(c2.motion.Position.x):F3}");

            var loop = Multi(); loop.id = "multi_loop"; loop.animKey = "multi_loop"; loop.autoNextActionId = "multi_loop"; loop.keepPlayingAnim = true;
            var c3 = Make(1, 1, "0", "multi_loop", loop);
            var w3 = World(c3);
            var fr = new RoomFrame();
            for (int i = 0; i < 20; i++) { fr.FrameId = ++_f; w3.Tick(fr); }
            long g0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) { fr.FrameId = ++_f; w3.Tick(fr); }
            long g1 = GC.GetAllocatedBytesForCurrentThread() - g0;
            Check(g1 == 0, $"多段 RootMotion 循环 300 帧托管分配 {g1} bytes");
        }

        Console.WriteLine("== 11. 朝向：权威状态在 ActionController，无输入组件也能转向；inverse 动作翻转；输入前/后按朝向映射 ==");
        {
            // 无输入组件、面朝左：攻击盒向左镜像，打到左侧目标，打不到右侧目标
            var a = Make(1, 1, "0", "atk", Attack("atk", normal, 8));
            a.FacingLeft = true;
            Check(a.input == null && a.FacingLeft, "无输入组件设置朝左生效");
            var left = Make(2, 2, "-0.9", "idle");
            var right = Make(3, 2, "0.9", "idle");
            var w = World(a, left, right); Step(w);
            Check(left.Hurt == HurtState.Stun && right.Hurt == HurtState.None, $"朝左只命中左侧目标: 左={left.Hurt}, 右={right.Hurt}");

            // inverse 动作：进入时翻转朝向
            var turn = Act("turn", "idle"); turn.inverse = true;
            var c = Make(4, 1, "0", "idle", turn);
            Check(!c.FacingLeft, "初始朝右");
            c.action.SetAllActions(c.action.AllActions, "turn");
            Check(c.FacingLeft, "进入 inverse 动作后朝左");

            // 有输入组件：输入只记录绝对方向；指令中的前/后在匹配时按朝向换算
            InputToCommand PushRight(bool facingLeft)
            {
                var p = Make(5, 1, "0", "idle");
                p.input = new InputToCommand();
                p.InputSlot = 0;
                p.FacingLeft = facingLeft;
                var pw = World(p);
                var rf = new RoomFrame { FrameId = ++_f, InputFrames = new Dictionary<byte, InputFrame>() };
                rf.InputFrames[0] = new InputFrame { FrameId = rf.FrameId, JoyStickDir = 0 };
                pw.Tick(rf);
                return p.input;
            }
            var ir = PushRight(false); var il = PushRight(true);
            Check(ir.CurrentDirection == CommandKey.Right && il.CurrentDirection == CommandKey.Right, $"推右与朝向无关，均记录为 Right: {ir.CurrentDirection}, {il.CurrentDirection}");
            var fwd = new ActionCommand(CommandKey.Forward, "0.2");
            var back = new ActionCommand(CommandKey.Backward, "0.2");
            Check(ir.ActionOccur(fwd, false) && !ir.ActionOccur(back, false), "朝右时推右匹配 [Forward]");
            Check(il.ActionOccur(back, true) && !il.ActionOccur(fwd, true), "朝左时推右匹配 [Backward]");

            // 8 方向：斜向推右上，相对方向 [ForwardUp] / [BackwardUp] 按朝向换算；正右 / 正上不被斜向满足
            InputToCommand PushUpRight(bool facingLeft)
            {
                var p = Make(6, 1, "0", "idle");
                p.input = new InputToCommand();
                p.InputSlot = 0;
                p.FacingLeft = facingLeft;
                var pw = World(p);
                var rf = new RoomFrame { FrameId = ++_f, InputFrames = new Dictionary<byte, InputFrame>() };
                rf.InputFrames[0] = new InputFrame { FrameId = rf.FrameId, JoyStickDir = JoystickDir.FromAxes(1, 1) };
                pw.Tick(rf);
                return p.input;
            }
            var dr = PushUpRight(false); var dl = PushUpRight(true);
            Check(dr.CurrentDirection == CommandKey.UpRight && dr.DirX == 1 && dr.DirY == 1, $"推右上记录为 UpRight, 分量 ({dr.DirX},{dr.DirY})");
            var fwdUp = new ActionCommand(CommandKey.ForwardUp, "0.2");
            var backUp = new ActionCommand(CommandKey.BackwardUp, "0.2");
            Check(dr.ActionOccur(fwdUp, false) && !dr.ActionOccur(backUp, false) && dl.ActionOccur(backUp, true) && !dl.ActionOccur(fwdUp, true), "右上：朝右匹配 [ForwardUp]，朝左匹配 [BackwardUp]");
            Check(!dr.IsHeld(CommandKey.Up) && !dr.IsHeld(CommandKey.Forward) && dr.IsHeld(CommandKey.Move), "斜向不满足正上 / 正前，满足 Move");
        }

        Console.WriteLine("== 12. move：按住摇杆持续移动、随摇杆转向、松开回 idle (配置走 ActionData DTO) ==");
        {
            var all = new FrameRange(0, 999);
            var idleCfg = new ActionData
            {
                id = "idle", animKey = "idle", autoNextActionId = "idle", keepPlayingAnim = true,
                beCancelledTag = new[] { new BeCancelledTagData { frameRange = all, cancelTag = new[] { "move" } } },
                inputAcceptance = new[] { new MoveInputAcceptanceData { range = all, rate = "0", turn = true } }
            }.ToRuntime();
            var moveCfg = new ActionData
            {
                id = "move", animKey = "move", autoNextActionId = "idle", keepPlayingAnim = true, autoTerminate = true,
                cancelTag = new[] { new CancelTag { tag = "move" } },
                beCancelledTag = new[] { new BeCancelledTagData { frameRange = all, cancelTag = new[] { "move" } } },
                commands = new[] { new ActionCommandData { keySequence = new[] { "Move" } } },
                inputAcceptance = new[] { new MoveInputAcceptanceData { range = all, rate = "1", turn = true } }
            }.ToRuntime();
            var p = Make(1, 1, "0", "idle", moveCfg);
            var acts = new List<ActionInfo>(p.action.AllActions); acts[0] = idleCfg; p.action.SetAllActions(acts, "idle");
            p.input = new InputToCommand(); p.InputSlot = 0;
            var w = World(p);
            int flips = 0; bool lastFacing = p.FacingLeft;
            string Run(byte dir, int n)
            {
                string ids = "";
                for (int i = 0; i < n; i++)
                {
                    var rf = new RoomFrame { FrameId = ++_f, InputFrames = new Dictionary<byte, InputFrame>() };
                    rf.InputFrames[0] = new InputFrame { FrameId = rf.FrameId, JoyStickDir = dir };
                    w.Tick(rf);
                    if (p.FacingLeft != lastFacing) { flips++; lastFacing = p.FacingLeft; }
                    ids += p.action.CurrentAction.id[0];
                }
                return ids;
            }
            string s0 = Run(255, 3);
            Check(s0 == "iii", $"无输入保持 idle: {s0}");
            float x0 = F(p.motion.Position.x);
            string s1 = Run(0, 10);
            float x1 = F(p.motion.Position.x);
            Check(s1.EndsWith("mmmmmmmm") && !p.FacingLeft && x1 > x0 + 0.8f, $"推右进入 move 并持续右移: {s1}, x {x0:F3}->{x1:F3}");
            string s2 = Run(4, 10);
            float x2 = F(p.motion.Position.x);
            Check(s2 == "mmmmmmmmmm" && p.FacingLeft && flips == 1 && x2 < x1 - 0.8f, $"推左立即转向且不闪烁: {s2}, 翻转 {flips} 次, x {x1:F3}->{x2:F3}");
            float y2 = F(p.motion.Position.y);
            string s3 = Run(2, 5);
            Check(s3 == "mmmmm" && p.FacingLeft && F(p.motion.Position.y) > y2 + 0.3f, $"推正上纵深移动、朝向不变: {s3}, 朝左={p.FacingLeft}");
            // 斜向两轴各自全速 (不归一化)：右下 6 帧的横向位移 = 正右 6 帧，纵深位移 = 正下 6 帧 (纵深保持在边界内)
            float xa = F(p.motion.Position.x);
            Run(0, 6);
            float dxAxis = F(p.motion.Position.x) - xa;
            float yb = F(p.motion.Position.y);
            Run(6, 6);
            float dyAxis = F(p.motion.Position.y) - yb;
            float xc = F(p.motion.Position.x), yc = F(p.motion.Position.y);
            Run(7, 6);
            float dxDiag = F(p.motion.Position.x) - xc, dyDiag = F(p.motion.Position.y) - yc;
            Check(dxAxis > 0.5f && dyAxis < -0.5f && System.Math.Abs(dxDiag - dxAxis) < 1e-4f && System.Math.Abs(dyDiag - dyAxis) < 1e-4f,
                $"斜向两轴独立全速: 轴向 ({dxAxis:F3},{dyAxis:F3}) 斜向 ({dxDiag:F3},{dyDiag:F3})");
            string s4 = Run(255, 5);
            float x4 = F(p.motion.Position.x);
            string s5 = Run(255, 3);
            Check(s4.EndsWith("iiii") && F(p.motion.Position.x) == x4, $"松开回 idle 且停止: {s4}");
        }

        Console.WriteLine("== 13. 浮空完整流程：toair -> inair -> fall -> 触地弹起(inair) -> fall -> 弹不起倒地 -> lie -> standup(无敌) -> idle(追加无敌) ==");
        {
            var launcher = normal; launcher.launchVelocity = "10";
            var a = Make(1, 1, "0", "atk", Attack("atk", launcher, 8));
            var b = Make(3, 1, "0", "idle", Attack("atk", normal, 8));
            var t = Make(2, 2, "0.9", "idle");
            var w = World(a, b, t);
            var seq = new List<string>();
            int bounces = 0; float impact1 = -1;
            bool standupProbed = false, afterProbed = false, finalProbed = false;
            for (int i = 0; i < 120; i++)
            {
                float vzBefore = F(t.motion.VelocityZ);
                Step(w);
                if ((t.LastPhysicsEvent & PhysicsEvent.Bounced) != 0)
                {
                    bounces++;
                    if (impact1 < 0) impact1 = F(t.motion.VelocityZ);
                }
                string id = t.action.CurrentAction.id;
                if (seq.Count == 0 || seq[seq.Count - 1] != id) seq.Add(id);

                // 起身期间：第三方攻击不产生命中
                if (!standupProbed && t.Hurt == HurtState.Standup && id == "standup")
                {
                    standupProbed = true;
                    Check(t.IsInvincible, "起身期间无敌");
                    b.action.SetAllActions(b.action.AllActions, "atk");
                    Step(w);
                    Check(w.HitEvents.Count == 0 && F(t.Hp) == 950f, $"起身期间不受命中: Hp={F(t.Hp)}");
                }
                // 起身结束回到 idle 后的追加无敌
                if (standupProbed && !afterProbed && t.Hurt == HurtState.None && id == "idle")
                {
                    afterProbed = true;
                    Check(t.InvincibleTicks > 0 && t.InvincibleTicks <= 15, $"起身后追加无敌 0.5s: 剩余 {t.InvincibleTicks} 帧");
                    b.action.SetAllActions(b.action.AllActions, "atk");
                    Step(w);
                    Check(w.HitEvents.Count == 0, "追加无敌期间不受命中");
                }
                if (afterProbed && !finalProbed && t.InvincibleTicks == 0)
                {
                    finalProbed = true;
                    b.action.SetAllActions(b.action.AllActions, "atk");
                    Step(w);
                    Check(w.HitEvents.Count == 1 && t.Hurt == HurtState.Stun, $"无敌结束后可被命中: {t.Hurt}");
                    break;
                }
            }
            string flow = string.Join(" > ", seq);
            Check(flow.StartsWith("hurt_toair > hurt_inair > hurt_fall > hurt_inair > hurt_fall > lie > standup > idle"), $"动作流: {flow}");
            Check(bounces == 1 && System.Math.Abs(impact1 - 5f) < 0.2f, $"弹起 1 次，速度约 10×0.5=5: 弹起 {bounces} 次, 速度 {impact1:F3}");
            Check(standupProbed && afterProbed && finalProbed, "起身/追加无敌/无敌结束 三段均已检查");
        }

        Console.WriteLine("== 14. 最小模板 action_template.json：加载 -> 受击动作齐全 -> idle/move -> 被击飞走完整受击流程 ==");
        {
            string json = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "action_template.json"));
            var container = System.Text.Json.JsonSerializer.Deserialize<ActionDataContainer>(json,
                new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
            var acts = new List<ActionInfo>();
            foreach (var d in container.data) acts.Add(d.ToRuntime());
            Check(acts.Count == 9 && HurtActionIds.FindMissing(acts).Count == 0, $"加载 {acts.Count} 个动作，通用受击动作齐全");

            var anim = new FrameAnimator();
            foreach (var act in acts) anim.RegisterAnim(act.animKey, Anim(act.id == "lie" ? 20 : act.id == "standup" ? 6 : act.id.StartsWith("hurt") ? 3 : 8, false));
            var ac = new ActionController { anim = anim };
            var p = new CharacterObj { action = ac, EntityId = 2, TeamId = 2, InputSlot = 0 };
            ac.Set((x, y) => p.HitRecords.Clear());
            int errors = Debug.ErrorCount;
            ac.SetAllActions(acts, "idle");
            Check(Debug.ErrorCount == errors, $"无加载报错 ({Debug.LastError})");
            p.input = new InputToCommand();
            p.motion.Reset(new FPVector3(FP.Parse("0.9"), FP.Zero, FP.Zero));

            var launcher = normal; launcher.launchVelocity = "10";
            var a = Make(1, 1, "0", "idle", Attack("atk", launcher, 8));
            var w = World(a, p);
            void Feed(byte dir, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    var rf = new RoomFrame { FrameId = ++_f, InputFrames = new Dictionary<byte, InputFrame>() };
                    rf.InputFrames[0] = new InputFrame { FrameId = rf.FrameId, JoyStickDir = dir };
                    w.Tick(rf);
                }
            }
            Feed(4, 6);
            Check(p.action.CurrentAction.id == "move" && p.FacingLeft && F(p.motion.Position.x) < 0.9f, $"推左：move、朝左、左移 x={F(p.motion.Position.x):F3}");
            Feed(255, 3);
            Check(p.action.CurrentAction.id == "idle", $"松开回 idle: {p.action.CurrentAction.id}");

            p.motion.Reset(new FPVector3(FP.Parse("0.9"), FP.Zero, FP.Zero));
            a.action.SetAllActions(a.action.AllActions, "atk");
            var seq = new List<string>();
            for (int i = 0; i < 90; i++)
            {
                Feed(255, 1);
                string id = p.action.CurrentAction.id;
                if (seq.Count == 0 || seq[seq.Count - 1] != id) seq.Add(id);
                if (seq.Count > 1 && id == "idle") break;
            }
            string flow = string.Join(" > ", seq);
            Check(flow == "hurt_toair > hurt_inair > hurt_fall > hurt_inair > hurt_fall > lie > standup > idle", $"受击流程: {flow}");
        }

        Console.WriteLine("== 15. 按键事件：按住不连发、松开触发、点按 [Attack, AttackRelease] ==");
        {
            var all = new FrameRange(0, 999);
            ActionInfo Cfg(string id, string cmd, string tag, string next, string[] cancelBy, bool loop = false) => new ActionData
            {
                id = id, animKey = id, autoNextActionId = next, keepPlayingAnim = loop,
                cancelTag = tag == null ? null : new[] { new CancelTag { tag = tag } },
                beCancelledTag = cancelBy == null ? null : new[] { new BeCancelledTagData { frameRange = all, cancelTag = cancelBy } },
                commands = cmd == null ? null : new[] { new ActionCommandData { keySequence = cmd.Split(','), validInSec = "0.2" } }
            }.ToRuntime();
            var idle = Cfg("idle", null, null, "idle", new[] { "atk1", "charge", "tap" }, true);
            var atk1 = Cfg("attack1", "Attack", "atk1", "idle", new[] { "atk2" });
            var atk2 = Cfg("attack2", "Attack", "atk2", "idle", null);
            var charge = Cfg("charge", "Skill1", "charge", "charge", new[] { "shot" }, true);
            var shot = Cfg("shot", "Skill1Release", "shot", "idle", null);
            var tap = Cfg("tap", "Skill2,Skill2Release", "tap", "idle", null);
            var p = Make(1, 1, "0", "idle", atk1, atk2, charge, shot, tap);
            var acts = new List<ActionInfo>(p.action.AllActions); acts[0] = idle; p.action.SetAllActions(acts, "idle");
            p.input = new InputToCommand(); p.InputSlot = 0;
            var w = World(p);
            string Feed(ButtonMask buttons, int n)
            {
                string ids = "";
                for (int i = 0; i < n; i++)
                {
                    var rf = new RoomFrame { FrameId = ++_f, InputFrames = new Dictionary<byte, InputFrame>() };
                    rf.InputFrames[0] = new InputFrame { FrameId = rf.FrameId, JoyStickDir = JoystickDir.None, Buttons = buttons };
                    w.Tick(rf);
                    string id = p.action.CurrentAction.id;
                    if (ids.Length == 0 || !ids.EndsWith(id)) ids += (ids.Length > 0 ? ">" : "") + id;
                }
                return ids;
            }
            string s1 = Feed(ButtonMask.Attack, 20);
            Check(!s1.Contains("attack2"), $"按住普攻 20 帧不连发: {s1}");
            Feed(ButtonMask.None, 3);
            string s2 = Feed(ButtonMask.Attack, 2) + "|" + Feed(ButtonMask.None, 1) + "|" + Feed(ButtonMask.Attack, 2);
            Check(s2.Contains("attack2"), $"松开再按接 attack2: {s2}");
            Feed(ButtonMask.None, 12);
            string s3 = Feed(ButtonMask.Skill1, 10);
            Check(s3 == "charge", $"按住技能1 蓄力: {s3}");
            string s4 = Feed(ButtonMask.None, 2);
            Check(s4.StartsWith("shot"), $"松开技能1 触发 shot (Skill1Release): {s4}");
            Feed(ButtonMask.None, 10);
            string s5 = Feed(ButtonMask.Skill2, 1) + "|" + Feed(ButtonMask.None, 1);
            Check(s5.EndsWith("tap"), $"点按技能2 触发 [Skill2, Skill2Release]: {s5}");
            Feed(ButtonMask.None, 10);
            string s6 = Feed(ButtonMask.Skill2, 10) + "|" + Feed(ButtonMask.None, 1);
            Check(!s6.Contains("tap"), $"按住超过 0.2 秒再松开不算点按: {s6}");
        }

        Console.WriteLine("== 16. 蓄力：按下 Skill1 进入蓄力，按住保持，松开按蓄力时长释放 lv1/lv2，蓄满自动释放 ==");
        {
            var all = new FrameRange(0, 999);
            var idle = new ActionData
            {
                id = "idle", animKey = "idle", autoNextActionId = "idle", keepPlayingAnim = true,
                beCancelledTag = new[] { new BeCancelledTagData { frameRange = all, cancelTag = new[] { "skill1_charge" } } }
            }.ToRuntime();
            var charge = new ActionData
            {
                id = "skill1_charge", animKey = "skill1_charge", autoNextActionId = "skill1_release_lv2",
                cancelTag = new[] { new CancelTag { tag = "skill1_charge" } },
                commands = new[] { new ActionCommandData { keySequence = new[] { "Skill1" }, validInSec = "0.2" } },
                beCancelledTag = new[]
                {
                    new BeCancelledTagData { frameRange = new FrameRange(0, 14), cancelTag = new[] { "skill1_release_lv1" } },
                    new BeCancelledTagData { frameRange = new FrameRange(15, 29), cancelTag = new[] { "skill1_release_lv2" } }
                }
            }.ToRuntime();
            ActionInfo Release(string id) => new ActionData
            {
                id = id, animKey = id, autoNextActionId = "idle",
                cancelTag = new[] { new CancelTag { tag = id } },
                commands = new[] { new ActionCommandData { keySequence = new[] { "Skill1Release" }, validInSec = "0.1" } }
            }.ToRuntime();
            var lv1 = Release("skill1_release_lv1"); var lv2 = Release("skill1_release_lv2");
            // 前一个动作：第 5~7 帧才开放取消到蓄力
            var block = new ActionData
            {
                id = "block", animKey = "block", autoNextActionId = "idle",
                beCancelledTag = new[] { new BeCancelledTagData { frameRange = new FrameRange(5, 7), cancelTag = new[] { "skill1_charge" } } }
            }.ToRuntime();

            CharacterObj P(out BattleWorld w)
            {
                var c = Make(1, 1, "0", "idle", charge, lv1, lv2, block);
                c.action.anim.RegisterAnim("skill1_charge", Anim(30, false));
                var acts = new List<ActionInfo>(c.action.AllActions); acts[0] = idle; c.action.SetAllActions(acts, "idle");
                c.input = new InputToCommand(); c.InputSlot = 0;
                w = World(c);
                return c;
            }
            string Feed(CharacterObj c, BattleWorld w, ButtonMask buttons, int n)
            {
                string ids = "";
                for (int i = 0; i < n; i++)
                {
                    var rf = new RoomFrame { FrameId = ++_f, InputFrames = new Dictionary<byte, InputFrame>() };
                    rf.InputFrames[0] = new InputFrame { FrameId = rf.FrameId, JoyStickDir = JoystickDir.None, Buttons = buttons };
                    w.Tick(rf);
                    string id = c.action.CurrentAction.id;
                    if (ids.Length == 0 || !ids.EndsWith(id)) ids += (ids.Length > 0 ? ">" : "") + id;
                }
                return ids;
            }
            var p1 = P(out var w1);
            string a = Feed(p1, w1, ButtonMask.Skill1, 8) + "|" + Feed(p1, w1, ButtonMask.None, 2);
            Check(a == "skill1_charge|skill1_release_lv1", $"短按 8 帧松开 -> lv1: {a}");
            var p2 = P(out var w2);
            string b2 = Feed(p2, w2, ButtonMask.Skill1, 20) + "|" + Feed(p2, w2, ButtonMask.None, 2);
            Check(b2 == "skill1_charge|skill1_release_lv2", $"按住 20 帧松开 -> lv2: {b2}");
            var p3 = P(out var w3);
            string c3 = Feed(p3, w3, ButtonMask.Skill1, 40);
            Check(c3.StartsWith("skill1_charge>skill1_release_lv2"), $"一直按住，蓄满 30 帧自动释放 lv2: {c3}");
            var p4 = P(out var w4);
            string d4 = Feed(p4, w4, ButtonMask.Skill1, 1) + "|" + Feed(p4, w4, ButtonMask.None, 2);
            Check(d4 == "skill1_charge|skill1_release_lv1", $"点按 -> 立即 lv1: {d4}");
            // 缓冲边界：取消窗口打开前按下又松开 -> 窗口打开时靠缓冲进入蓄力，松开记录跨动作保留 -> 立即释放 lv1
            var p5 = P(out var w5);
            p5.action.SetAllActions(p5.action.AllActions, "block");
            string e5 = Feed(p5, w5, ButtonMask.None, 2) + "|" + Feed(p5, w5, ButtonMask.Skill1, 1) + "|" + Feed(p5, w5, ButtonMask.None, 12);
            Check(e5.Contains("skill1_charge>skill1_release_lv1") && !e5.Contains("lv2"), $"缓冲中点按：进入蓄力后立即放出 lv1: {e5}");
            // 连按：按下-松开-再按住，第一次的松开被第二次按下作废，蓄力等待第二次松开
            var p6 = P(out var w6);
            p6.action.SetAllActions(p6.action.AllActions, "block");
            string f6 = Feed(p6, w6, ButtonMask.None, 1) + "|" + Feed(p6, w6, ButtonMask.Skill1, 1) + "|" + Feed(p6, w6, ButtonMask.None, 1)
                      + "|" + Feed(p6, w6, ButtonMask.Skill1, 12);
            bool stillCharging = p6.action.CurrentAction.id == "skill1_charge";
            string g6 = Feed(p6, w6, ButtonMask.None, 2);
            Check(stillCharging && g6.StartsWith("skill1_release"), $"连按后按住：保持蓄力直到第二次松开: {f6} | 松开后 {g6}");
        }

        Console.WriteLine(_fail == 0 ? "\n全部通过" : $"\n失败 {_fail} 项");
        return _fail == 0 ? 0 : 1;
    }
}
