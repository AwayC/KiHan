using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using KiHan.Logic;
using UnityEngine;

/// <summary>
/// M6：技能 (CD / 多段 / 查克拉 / 替身)、动作事件、生成物 (投射物 / 分身) 与分身大脑
/// </summary>
static class Program
{
    static int _fail;
    static void Check(bool ok, string msg) { Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {msg}"); if (!ok) _fail++; }

    const byte Slot = 1;
    static uint _f;
    static readonly FrameRange All = new FrameRange(0, 999);

    static readonly Dictionary<string, int> Frames = new Dictionary<string, int>
    {
        { "idle", 10 }, { "lie", 20 }, { "standup", 6 }, { "s1", 10 }, { "m1", 6 }, { "m2", 6 }, { "sub", 5 }, { "atk", 8 },
        { "summon", 6 }, { "proj", 8 }, { "fxact", 8 }, { "vanish", 4 }, { "kf", 10 }
    };

    static AnimationFrameData Anim(int frames, bool hit)
    {
        var lib = new CharacterSpriteLibrary();
        var a = new AnimationFrameData { Library = lib };
        for (int i = 0; i < frames; i++)
        {
            var fd = new SpriteFrameData();
            fd.HurtBoxes.Add(new LogicBox(new Vector2Int(0, 80), new Vector2Int(60, 160), 40));
            if (hit) fd.HitBoxes.Add(new LogicBox(new Vector2Int(60, 80), new Vector2Int(120, 160), 40));
            lib.AllFrames.Add(fd);
            a.Steps.Add(new AnimationFrameData.AnimStep { FrameIndex = i, Duration = 1 });
        }
        return a;
    }

    static ActionCommandData[] Cmd(string key) => new[] { new ActionCommandData { press = new[] { key }, validInSec = "0.2" } };
    static CancelTag[] Tag(string tag) => new[] { new CancelTag { tag = tag } };
    static AttackBoxTurnOnInfo[] Box(int from, int to, int phase = 0) => new[] { new AttackBoxTurnOnInfo { frameRanges = new[] { new FrameRange(from, to) }, attackPhase = phase } };

    static List<ActionInfo> Actions(string summonSpawnId)
    {
        AttackData Hit(string dmg, int times) => new AttackData { attack = dmg, hitStun = "0.3", canHitSameTarget = times, hitSameTargetDelay = "0" };
        var data = new List<ActionData>
        {
            new ActionData
            {
                id = "idle", animKey = "idle", keepPlayingAnim = true, autoNextActionId = "idle",
                beCancelledTag = new[] { new BeCancelledTagData { frameRange = All, cancelTag = new[] { "skill", "attack" } } }
            },
            new ActionData { id = "hurt", animKey = "hurt" }, new ActionData { id = "grabbed", animKey = "hurt" },
            new ActionData { id = "float", animKey = "hurt" }, new ActionData { id = "fall", animKey = "hurt" },
            new ActionData { id = "lie", animKey = "lie" },
            new ActionData { id = "standup", animKey = "standup" },
            new ActionData
            {
                id = "s1", animKey = "s1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                attackPhase = Box(2, 4), attacks = new[] { Hit("10", 3) }
            },
            new ActionData
            {
                id = "m1", animKey = "m1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill2"),
                scripts = new[] { Stage("0.5", "atlas#stage2") }
            },
            new ActionData { id = "m2", animKey = "m2", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill2") },
            new ActionData { id = "sub", animKey = "sub", breakHurt = true, commands = Cmd("Substitution") },
            new ActionData
            {
                id = "atk", animKey = "atk", autoNextActionId = "idle", cancelTag = Tag("attack"), commands = Cmd("Attack"),
                attackPhase = Box(1, 3), attacks = new[] { Hit("10", 1) }
            },
            new ActionData
            {
                id = "summon", animKey = "summon", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Secret"),
                events = new[] { new ActionEventData { frame = 1, type = "Spawn", spawnId = summonSpawnId } }
            },
            new ActionData
            {
                id = "proj", animKey = "proj", attackPhase = Box(0, 7), attacks = new[] { Hit("5", 1) },
                events = new[] { new ActionEventData { frame = 7, type = "Effect", asset = "fx_end" } }
            },
            new ActionData
            {
                id = "fxact", animKey = "fxact", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Ultimate"),
                events = new[]
                {
                    new ActionEventData { frame = 0, type = "Effect", asset = "fx_a", anchor = "hand", bind = true, stopOnActionEnd = true },
                    new ActionEventData { frame = 1, type = "Sound", asset = "sfx_a", stopOnActionEnd = true },
                    new ActionEventData { frame = 3, type = "Camera", asset = CameraEffects.ImpactHeavy },
                    new ActionEventData { frame = 3, type = "Cinematic", asset = "cin_a", audience = "Self" },
                    new ActionEventData { frame = 3, type = "Effect", asset = "fx_w", anchor = "hand", offset = new Vector2Int(50, 0), bind = true, worldPos = new EventPositionData { x = "1.5", z = "2" } }
                }
            },
            new ActionData { id = "vanish", animKey = "vanish" },
            new ActionData
            {
                id = "kf", animKey = "kf", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Summon"),
                attackPhase = Box(1, 1),
                attacks = new[]
                {
                    new AttackData
                    {
                        attack = "1", hitStun = "0.3", canHitSameTarget = 1,
                        selfActionChange = new ActionChangeData { changeType = 1, param = "kf_v", priority = 50, keepFrame = true }
                    }
                }
            },
            new ActionData { id = "kf_v", animKey = "kf", autoNextActionId = "idle" },
            new ActionData
            {
                id = "buffact", animKey = "fxact", autoNextActionId = "idle",
                scripts = new[]
                {
                    new ActionScriptData { frame = 0, op = "addBuff", buff = "haste" },
                    new ActionScriptData { frame = 2, op = "addBuff", buff = "haste" }
                }
            },
        };

        var list = new List<ActionInfo>();
        foreach (ActionData d in data) list.Add(d.ToRuntime());
        return list;
    }

    /// <summary>
    /// 多段技能 multi 的换段指令：m1 第 0 帧把按键换成 m2，返还 0.5 秒
    /// </summary>
    static ActionScriptData Stage(string window, string icon = null) =>
        new ActionScriptData { frame = 0, op = "skillStage", skill = "multi", action = "m2", window = window, cdReturn = "0.5", icon = icon };

    /// <summary>
    /// 按 id 替换动作库中的动作，没有则追加
    /// </summary>
    static List<ActionInfo> Replace(List<ActionInfo> actions, params ActionData[] data)
    {
        foreach (ActionData d in data)
        {
            int i = actions.FindIndex(a => a.id == d.id);
            if (i >= 0) actions[i] = d.ToRuntime();
            else actions.Add(d.ToRuntime());
        }
        return actions;
    }

    static SkillInfo[] Skills()
    {
        var data = new[]
        {
            new SkillData { id = "s1", button = "Skill1", cooldown = "1", actionId = "s1" },
            new SkillData { id = "multi", button = "Skill2", icon = "atlas#skill", cooldown = "2", actionId = "m1" },
            new SkillData { id = "sub", button = "Substitution", cooldown = "1", requireHurt = true, actionId = "sub" },
            new SkillData { id = "summon", button = "Secret", cooldown = "1", actionId = "summon" },
        };
        var skills = new SkillInfo[data.Length];
        for (int i = 0; i < data.Length; i++) skills[i] = data[i].ToRuntime();
        return skills;
    }

    static CharacterObj Make(int id, int team, string x, List<ActionInfo> actions, bool withInput)
    {
        var anim = new FrameAnimator();
        foreach (ActionInfo a in actions)
        {
            if (anim.HasAnim(a.animKey)) continue;
            int frames = Frames.TryGetValue(a.animKey, out int n) ? n : 3;
            anim.RegisterAnim(a.animKey, Anim(frames, a.attacks != null && a.attacks.Length > 0));
        }
        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = id, TeamId = team, CharacterId = 1 };
        if (withInput) cha.input = new InputToCommand();
        ac.SetAllActions(actions, "idle");
        cha.motion.Reset(new FPVector3(FP.Parse(x), FP.Zero, FP.Zero));
        return cha;
    }

    static CharacterObj Player(List<ActionInfo> actions)
    {
        var p = Make(1, 1, "0", actions, true);
        p.InputSlot = Slot;
        var skills = new SkillController();
        skills.Setup(Skills(), actions, 4);
        p.Skills = skills;
        return p;
    }

    /// <summary>
    /// 测试用生成物池：按 SpawnInfo 构造实例，回收后复用
    /// </summary>
    class TestPool : ISpawnProvider
    {
        public readonly Dictionary<string, SpawnInfo> Infos = new Dictionary<string, SpawnInfo>();
        public readonly Dictionary<string, Stack<CharacterObj>> Free = new Dictionary<string, Stack<CharacterObj>>();
        public List<ActionInfo> Actions;
        public int Created;

        public CharacterObj Acquire(CharacterObj source, string spawnId)
        {
            if (!Infos.TryGetValue(spawnId, out SpawnInfo info)) return null;
            if (Free.TryGetValue(spawnId, out var stack) && stack.Count > 0) return stack.Pop();
            Created++;
            var e = Make(0, 0, "0", Actions, info.type == SpawnType.Actor);
            e.Spawn = info;
            if (info.type == SpawnType.Actor) e.Brain = new CloneBrain();
            return e;
        }

        public void Release(CharacterObj entity)
        {
            if (!Free.TryGetValue(entity.Spawn.id, out var stack)) Free[entity.Spawn.id] = stack = new Stack<CharacterObj>();
            stack.Push(entity);
        }

        public void Prewarm(string spawnId, int count)
        {
            var created = new List<CharacterObj>();
            for (int i = 0; i < count; i++) created.Add(Acquire(null, spawnId));
            foreach (var e in created) Release(e);
        }
    }

    static readonly RoomFrame Frame = new RoomFrame { InputFrames = new Dictionary<byte, InputFrame> { { Slot, new InputFrame { JoyStickDir = JoystickDir.None } } } };

    static void Step(BattleWorld w, ButtonMask buttons = ButtonMask.None, int n = 1)
    {
        for (int i = 0; i < n; i++)
        {
            Frame.FrameId = ++_f;
            Frame.InputFrames[Slot].Buttons = buttons;
            w.Tick(Frame);
        }
    }

    /// <summary>
    /// 推进直到角色回到 idle (最多 limit 帧)，返回推进的帧数
    /// </summary>
    static int UntilIdle(BattleWorld w, CharacterObj c, int limit = 120)
    {
        int n = 0;
        while (c.action.CurrentAction.id != "idle" && n < limit) { Step(w); n++; }
        return n;
    }

    static BattleWorld World(params CharacterObj[] chars)
    {
        var w = new BattleWorld(new MapLogic());
        foreach (var c in chars) w.AddCharacter(c);
        return w;
    }

    static int Main()
    {
        int s1 = 0, multi = 1, sub = 2, summon = 3;

        Console.WriteLine("== 1. 单段技能：释放时立即开始计算完整 CD，CD 中按键无效，CD 结束可再次释放 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "s1" && p.Skills.CastingSkill == s1 && p.Skills.GetCooldown(s1) == 30,
                $"按下即进入 s1 并开始完整 CD 1 秒×30=30: 动作={p.action.CurrentAction.id} CD={p.Skills.GetCooldown(s1)}");
            int n = UntilIdle(w, p);
            Check(n == 10 && p.Skills.GetCooldown(s1) == 20, $"s1 播完 (预期 10 帧) 回到 idle，释放期间 CD 照常倒计时: 用时 {n} 帧, CD={p.Skills.GetCooldown(s1)} (预期 20)");
            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "idle" && p.Skills.GetCooldown(s1) == 19, $"CD 中按键无效: 动作={p.action.CurrentAction.id}, CD={p.Skills.GetCooldown(s1)} (预期 19)");
            Step(w, ButtonMask.None, 19);
            Check(p.Skills.GetCooldown(s1) == 0, $"再过 19 帧 CD 归零: {p.Skills.GetCooldown(s1)}");
            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "s1", $"CD 结束后再次释放: {p.action.CurrentAction.id}");
        }

        Console.WriteLine("== 2. 多段技能 (动作指令 skillStage)：入口动作第 0 帧换段并开始接段计时 (释放期间照常倒数)，时限内同一按键放下一段并开始完整 CD；超时恢复按键并返还 cdReturn；skillEnd 离开时返还 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            Step(w, ButtonMask.Skill2);
            Check(p.action.CurrentAction.id == "m1" && p.Skills.CastingSkill == multi && p.Skills.GetStageAction(multi) == "m2"
                && p.Skills.GetWindow(multi) == 15 && p.Skills.GetWindowTotal(multi) == 15 && p.Skills.GetCooldown(multi) == 0,
                $"按下即进入 m1，第 0 帧 skillStage 当帧换段 (不闪 CD)：下一段=m2、时限 0.5×30=15、CD 0: 动作={p.action.CurrentAction.id} 段={p.Skills.GetStageAction(multi)} 时限={p.Skills.GetWindow(multi)} CD={p.Skills.GetCooldown(multi)}");
            int m1Ticks = UntilIdle(w, p);
            Check(p.Skills.GetWindow(multi) == 15 - m1Ticks && p.Skills.GetCooldown(multi) == 0,
                $"m1 释放期间时限照常倒数: 时限={p.Skills.GetWindow(multi)} (预期 15-{m1Ticks}) CD={p.Skills.GetCooldown(multi)}");
            Step(w, ButtonMask.Skill2);
            Check(p.action.CurrentAction.id == "m2", $"时限内同一按键放 m2 (入口 m1 被准入拦截): {p.action.CurrentAction.id}");
            Check(p.Skills.GetCooldown(multi) == 60 && p.Skills.GetStageAction(multi) == "" && p.Skills.GetWindow(multi) == 0 && p.Skills.CastingSkill == multi,
                $"放下一段时开始完整 CD 2×30=60、恢复按键、时限清零: CD={p.Skills.GetCooldown(multi)} 段=\"{p.Skills.GetStageAction(multi)}\" 时限={p.Skills.GetWindow(multi)}");
            int m2Ticks = UntilIdle(w, p);
            Check(p.Skills.GetCooldown(multi) == 60 - m2Ticks, $"m2 释放期间 CD 照常倒计时: CD={p.Skills.GetCooldown(multi)} (预期 60-{m2Ticks})");

            var q = Player(Actions("proj"));
            var w2 = World(q);
            Step(w2, ButtonMask.Skill2);
            Step(w2, ButtonMask.None, 14);
            Check(q.Skills.GetWindow(multi) == 1 && q.Skills.GetCooldown(multi) == 0, $"时限最后一帧前仍在换段 (从释放起算 14 帧): 时限={q.Skills.GetWindow(multi)} CD={q.Skills.GetCooldown(multi)}");
            Step(w2);
            Check(q.Skills.GetCooldown(multi) == 45 && q.Skills.GetStageAction(multi) == "" && q.Skills.GetWindow(multi) == 0,
                $"超时：恢复按键，CD = 60 - 返还 0.5×30=15 = 45: CD={q.Skills.GetCooldown(multi)} 段=\"{q.Skills.GetStageAction(multi)}\"");
            Step(w2, ButtonMask.Skill2);
            Check(q.action.CurrentAction.id == "idle", $"超时后 CD 中 m1 / m2 都不能释放: {q.action.CurrentAction.id}");

            // 时限短于 m1：释放期间就超时，进入 CD；放完离开时不再打开时限、不重复计算 CD
            var shortActions = Replace(Actions("proj"), new ActionData
            {
                id = "m1", animKey = "m1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill2"),
                scripts = new[] { Stage("0.1") }
            });
            var r = Player(shortActions);
            var w3 = World(r);
            Step(w3, ButtonMask.Skill2);
            Step(w3, ButtonMask.None, 3);
            Check(r.action.CurrentAction.id == "m1" && r.Skills.CastingSkill == multi && r.Skills.GetWindow(multi) == 0 && r.Skills.GetStageAction(multi) == "" && r.Skills.GetCooldown(multi) == 45,
                $"m1 释放期间时限 0.1×30=3 帧到期：开始 CD 60-15=45: 动作={r.action.CurrentAction.id} 释放中={r.Skills.CastingSkill} 时限={r.Skills.GetWindow(multi)} CD={r.Skills.GetCooldown(multi)}");
            int rest = UntilIdle(w3, r);
            Check(rest == 3 && r.Skills.GetWindow(multi) == 0 && r.Skills.GetCooldown(multi) == 45 - rest,
                $"m1 放完离开：不再打开时限、CD 不重新计算: 用时 {rest} 帧 (预期 3) 时限={r.Skills.GetWindow(multi)} CD={r.Skills.GetCooldown(multi)} (预期 45-{rest})");
            Step(w3, ButtonMask.Skill2);
            Check(r.action.CurrentAction.id == "idle", $"CD 中 m1 / m2 都不能释放: {r.action.CurrentAction.id}");

            // skillEnd：m2 离开时立即开始 CD 并返还 cdReturn (覆盖进入 m2 时开始的完整 CD)
            var endActions = Replace(Actions("proj"), new ActionData
            {
                id = "m2", animKey = "m2", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill2"),
                scripts = new[] { new ActionScriptData { on = "exit", op = "skillEnd", skill = "multi", cdReturn = "1" } }
            });
            var e = Player(endActions);
            var w4 = World(e);
            Step(w4, ButtonMask.Skill2);
            UntilIdle(w4, e);
            Step(w4, ButtonMask.Skill2);
            Step(w4);
            Check(e.action.CurrentAction.id == "m2" && e.Skills.GetCooldown(multi) == 59, $"m2 释放中仍是完整 CD 倒计时: 动作={e.action.CurrentAction.id} CD={e.Skills.GetCooldown(multi)} (预期 59)");
            int endTicks = UntilIdle(w4, e);
            Check(e.action.CurrentAction.id == "idle" && e.Skills.GetCooldown(multi) == 30,
                $"离开 m2 时 exit 指令 skillEnd：CD = 60 - 返还 1×30 = 30 (未按完整 CD 继续倒数到 {59 - endTicks}): CD={e.Skills.GetCooldown(multi)}");
        }

        Console.WriteLine("== 3. 查克拉：技能命中获得 1 颗，同一轮多次命中只获得 1 颗；普攻不获得；CD 刷新后可再次获得 ==");
        {
            var actions = Actions("proj");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            t.hpMode = HpMode.Refill;
            var w = World(p, t);
            int hits = 0;
            Step(w, ButtonMask.Skill1);
            for (int i = 0; i < 10; i++) { Step(w); hits += w.HitEvents.Count; }
            Check(hits == 3 && p.Skills.Chakra == 1, $"s1 三帧各命中一次 (canHitSameTarget=3)，只获得 1 颗: 命中 {hits} 次, 查克拉 {p.Skills.Chakra}");
            UntilIdle(w, p);
            Step(w, ButtonMask.Attack);
            hits = 0;
            for (int i = 0; i < 8; i++) { Step(w); hits += w.HitEvents.Count; }
            Check(hits == 1 && p.Skills.Chakra == 1, $"普攻命中不获得查克拉: 命中 {hits}, 查克拉 {p.Skills.Chakra}");
            Step(w, ButtonMask.None, 40);
            Check(p.Skills.GetCooldown(s1) == 0 && p.Skills.IsChakraReady(s1), $"CD 结束刷新可获得状态: CD={p.Skills.GetCooldown(s1)} ready={p.Skills.IsChakraReady(s1)}");
            Step(w, ButtonMask.Skill1);
            Step(w, ButtonMask.None, 6);
            Check(p.Skills.Chakra == 2, $"刷新后再次命中 +1: {p.Skills.Chakra}");
        }

        Console.WriteLine("== 4. 投射物 (Simple)：动作事件生成、继承阵营与朝向、不可受击、命中归属主人技能、动作链走完回收并复用 ==");
        {
            var actions = Actions("proj");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            t.hpMode = HpMode.Refill;
            var pool = new TestPool { Actions = actions };
            pool.Infos["proj"] = new SpawnData { id = "proj", type = "Simple", initialActionId = "proj", lifeTime = "3" }.ToRuntime();
            var w = World(p, t);
            w.Spawner = pool;

            Step(w, ButtonMask.Secret);
            Step(w);
            Check(w.Spawned.Count == 0, "第 0 帧尚未生成");
            Step(w);
            CharacterObj proj = w.Spawned.Count == 1 ? w.Spawned[0] : null;
            Check(proj != null && proj.EntityId == BattleWorld.FirstSpawnEntityId && proj.TeamId == 1 && proj.Owner == p && proj.SourceSkill == summon && !proj.FacingLeft,
                $"处理到第 1 帧时生成：编号={proj?.EntityId} 阵营={proj?.TeamId} 主人={proj?.Owner?.EntityId} 来源技能={proj?.SourceSkill} (预期 1000/1/1/{summon})");
            Check(w.Characters.Count == 3 && proj.action.CurrentAction.id == "proj", $"加入战场并进入初始动作: 实体数={w.Characters.Count} 动作={proj?.action.CurrentAction.id}");

            Step(w);
            Check(w.HitEvents.Count == 1 && w.HitEvents[0].AttackerId == 1000 && w.HitEvents[0].OwnerId == 1, $"投射物命中，归属主人: 攻击者={(w.HitEvents.Count > 0 ? w.HitEvents[0].AttackerId : -1)} 主人={(w.HitEvents.Count > 0 ? w.HitEvents[0].OwnerId : -1)}");
            Check(p.Skills.Chakra == 1 && !p.Skills.IsChakraReady(summon), $"投射物命中为主人获得查克拉 (按来源技能计): {p.Skills.Chakra}");

            t.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 });
            t.FacingLeft = true;
            bool hitProj = false;
            int despawnTick = -1, fxEndTick = -1;
            for (int i = 0; i < 10 && despawnTick < 0; i++)
            {
                Step(w);
                foreach (var e in w.HitEvents) if (e.TargetId == 1000) hitProj = true;
                foreach (var v in w.ViewEvents) if (v.EntityId == 1000 && v.Asset == "fx_end") fxEndTick = i;
                if (w.Despawned.Contains(1000)) despawnTick = i;
            }
            Check(!hitProj, "投射物没有受击");
            Check(despawnTick == 6 && fxEndTick == despawnTick && w.Characters.Count == 2,
                $"8 帧动作走完当帧回收 (最后一帧的事件同帧发出): 回收于第 {despawnTick} 帧 (预期 6), 事件于第 {fxEndTick} 帧, 实体数={w.Characters.Count}");

            Step(w, ButtonMask.None, 30);
            Step(w, ButtonMask.Secret);
            Step(w, ButtonMask.None, 2);
            Check(w.Spawned.Count == 1 && w.Spawned[0] == proj && w.Spawned[0].EntityId == 1001 && pool.Created == 1,
                $"再次生成复用同一实例、编号递增: 编号={(w.Spawned.Count > 0 ? w.Spawned[0].EntityId : -1)} 新建实例数={pool.Created}");
        }

        Console.WriteLine("== 5. 生成与回收循环确定性 + 预热后 0 GC ==");
        {
            long Run(bool measure, out long alloc)
            {
                var actions = Actions("proj");
                var p = Player(actions);
                var t = Make(2, 2, "0.9", actions, false);
                t.hpMode = HpMode.Refill;
                var pool = new TestPool { Actions = actions };
                pool.Infos["proj"] = new SpawnData { id = "proj", type = "Simple", initialActionId = "proj" }.ToRuntime();
                pool.Prewarm("proj", 2);
                var w = World(p, t);
                w.Spawner = pool;
                long hash = 17;
                void Loop(int n)
                {
                    for (int i = 0; i < n; i++)
                    {
                        Step(w, (i % 50) == 0 ? ButtonMask.Secret : ButtonMask.None);
                        for (int k = 0; k < w.Characters.Count; k++)
                        {
                            CharacterObj c = w.Characters[k];
                            hash = hash * 31 + c.EntityId;
                            hash = hash * 31 + c.motion.Position.x.RawValue;
                        }
                        hash = hash * 31 + t.Hp.RawValue;
                    }
                }
                Loop(100);
                long before = GC.GetAllocatedBytesForCurrentThread();
                Loop(300);
                alloc = GC.GetAllocatedBytesForCurrentThread() - before;
                return hash ^ w.NextEntityId;
            }
            long h1 = Run(false, out long a1), h2 = Run(false, out _);
            Check(h1 == h2, $"两次运行状态哈希一致: {h1} / {h2}");
            Check(a1 == 0, $"预热后 300 帧 (含 6 次生成与回收) 托管分配 {a1} bytes");
        }

        Console.WriteLine("== 6. 动作事件：按帧触发一次、记录触发时的动作序号；从中途帧切入不补发之前的事件 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            var seen = new List<string>();
            int serialAtFx = -1;
            ViewEvent cin = default;
            ViewEvent fxw = default;
            Step(w, ButtonMask.Ultimate);
            for (int i = 0; i < 10; i++)
            {
                Step(w);
                foreach (var v in w.ViewEvents)
                {
                    seen.Add($"{i}:{v.Type}:{v.Asset}");
                    if (v.Asset == "fx_a") serialAtFx = v.ActionSerial;
                    if (v.Asset == "cin_a") cin = v;
                    if (v.Asset == "fx_w") fxw = v;
                }
            }
            string s = string.Join(",", seen);
            Check(s == "0:Effect:fx_a,1:Sound:sfx_a,3:Camera:impact_heavy,3:Cinematic:cin_a,3:Effect:fx_w", $"第 0 帧特效、第 1 帧音效、第 3 帧镜头、演出与世界坐标特效各触发一次: {s}");
            Check(cin.Audience == CinematicAudience.Self && cin.OwnerSlot == Slot, $"演出事件带观看者与主人槽位: {cin.Audience} / {cin.OwnerSlot}");
            Check(fxw.Position == new FPVector3(FP.Parse("1.5"), FP.Zero, FP.Parse("2")) && !fxw.Bind,
                $"worldPos 特效在指定逻辑坐标生成，忽略锚点 / 偏移且不跟随角色: pos={fxw.Position} bind={fxw.Bind}");
            Check(serialAtFx >= 0 && serialAtFx != p.action.ActionSerial, $"事件携带动作序号，动作结束后序号已改变 (表现层据此停止特效): 事件={serialAtFx} 当前={p.action.ActionSerial}");

            p.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "fxact", priority = 200, fromFrame = 2 });
            Step(w);
            seen.Clear();
            for (int i = 0; i < 8; i++) { Step(w); foreach (var v in w.ViewEvents) seen.Add(v.Asset); }
            Check(string.Join(",", seen) == "impact_heavy,cin_a,fx_w", $"从第 2 帧切入：第 0、1 帧事件不补发: {string.Join(",", seen)}");
        }

        Console.WriteLine("== 7. keepFrame：命中后切换到同一动画的变体动作，从当前帧继续播放 ==");
        {
            var actions = Actions("proj");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            var w = World(p, t);
            Step(w, ButtonMask.Summon);
            Step(w, ButtonMask.None, 2);
            Check(p.action.CurrentAction.id == "kf_v" && p.action.anim.ElapsedFrames == 2,
                $"第 1 帧命中 -> kf_v 从第 2 帧继续: 动作={p.action.CurrentAction.id} 帧={p.action.anim.ElapsedFrames}");
            int n = UntilIdle(w, p);
            Check(n == 8, $"变体播完剩余 10-2=8 帧后回到 idle: {n}");
        }

        Console.WriteLine("== 8. 替身：只能在受击状态使用，解除受击并以起身无敌播放替身动作，播完追加起身后无敌，之后开始 CD ==");
        {
            var actions = Actions("proj");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            t.FacingLeft = true;
            var w = World(p, t);
            Step(w, ButtonMask.Substitution);
            Check(p.action.CurrentAction.id == "idle" && p.Skills.GetCooldown(sub) == 0, $"非受击状态按替身无效: {p.action.CurrentAction.id}");
            Step(w);

            t.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 });
            Step(w, ButtonMask.None, 3);
            Check(p.Hurt == HurtState.Stun && p.action.CurrentAction.id == "hurt", $"被普攻命中进入僵直: {p.Hurt} {p.action.CurrentAction.id}");
            Step(w, ButtonMask.Substitution);
            Check(p.action.CurrentAction.id == "sub" && p.Hurt == HurtState.Standup && p.IsInvincible && p.StunTicks == 0,
                $"僵直中按替身：进入替身动作、起身无敌: {p.action.CurrentAction.id} {p.Hurt} 无敌={p.IsInvincible}");

            t.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 });
            int hits = 0, n = 0;
            while (p.action.CurrentAction.id == "sub" && n < 20) { Step(w); hits += w.HitEvents.Count; n++; }
            Check(hits == 0, $"替身期间不受命中: {hits}");
            Check(n == 5 && p.Hurt == HurtState.None && p.InvincibleTicks == 15 && p.action.CurrentAction.id == "idle",
                $"替身动作 5 帧播完回到 idle，追加 0.5×30=15 帧无敌: 用时 {n}, {p.Hurt}, 无敌 {p.InvincibleTicks}");
            Check(p.Skills.GetCooldown(sub) == 30 - n, $"释放替身时开始 CD 1×30=30，播完时已倒计时 {n} 帧: {p.Skills.GetCooldown(sub)}");

            p.EnterLying();
            Step(w);
            Step(w, ButtonMask.Substitution);
            Check(p.action.CurrentAction.id == "lie", $"CD 中倒地按替身无效: {p.action.CurrentAction.id}");
            Step(w, ButtonMask.None, 30);
            var q = Player(actions);
            var w2 = World(q);
            q.EnterLying();
            Step(w2);
            Step(w2, ButtonMask.Substitution);
            Check(q.action.CurrentAction.id == "sub" && q.Hurt == HurtState.Standup, $"倒地中按替身立即起身无敌: {q.action.CurrentAction.id} {q.Hurt}");
        }

        Console.WriteLine("== 8b. 替身落地 + 虚化 + 烟雾；死亡后倒地不再起身、不能替身 ==");
        {
            var actions = Actions("proj");
            var p = Player(actions);
            var w = World(p);
            p.motion.Launch(FP.Parse("6"));
            p.EnterLaunch();
            Step(w, ButtonMask.None, 4);
            FP airZ = p.motion.Position.z;
            Check(p.Hurt == HurtState.Launch && airZ > FP.Zero, $"击飞到空中: {p.Hurt} z={F(airZ)}");

            Step(w, ButtonMask.Substitution);
            int smokes = 0;
            foreach (var v in w.ViewEvents) if (v.Type == ActionEventType.Effect && v.Asset == CharacterObj.SubstitutionSmokeEffect) smokes++;
            Check(p.action.CurrentAction.id == "sub" && p.Hurt == HurtState.Standup, $"空中替身进入替身动作: {p.action.CurrentAction.id} {p.Hurt}");
            Check(p.motion.Position.z == FP.Zero && p.motion.VelocityZ == FP.Zero && !p.IsAirborne,
                $"空中替身直接回到地面并清除速度: z={F(p.motion.Position.z)} vz={F(p.motion.VelocityZ)}");
            Check(smokes == 2, $"空中替身在原位置与落点各放一次烟雾: {smokes}");
            Check(p.IsPhantom && p.IsInvincible, $"替身后虚化且无敌: 虚化={p.IsPhantom} 无敌={p.IsInvincible}");

            int n = 0;
            while (p.IsPhantom && n < 60) { Step(w); n++; }
            Check(p.action.CurrentAction.id == "idle" && !p.IsInvincible && n == 5 + 15,
                $"替身动作 5 帧 + 起身后无敌 15 帧结束后解除虚化，站在 idle: 用时 {n}, {p.action.CurrentAction.id}");

            var q = Player(actions);
            var w2 = World(q);
            q.EnterLying();
            Step(w2);
            q.TakeDamage(q.maxHp);
            Step(w2, ButtonMask.Substitution);
            Check(q.action.CurrentAction.id == "lie", $"死亡后倒地按替身无效: {q.action.CurrentAction.id}");
            Step(w2, ButtonMask.None, 90);
            Check(q.IsLying && q.action.CurrentAction.id == "lie", $"死亡后 3 秒仍倒地不起身: {q.Hurt} {q.action.CurrentAction.id}");

            var r = Player(actions);
            var w3 = World(r);
            r.TakeDamage(r.maxHp);
            Step(w3, ButtonMask.None, 2);
            Check(r.IsLying && r.action.CurrentAction.id == "lie", $"站立时死亡直接倒地: {r.Hurt} {r.action.CurrentAction.id}");

            var s = Player(actions);
            var w4 = World(s);
            Frame.InputFrames[Slot].JoyStickDir = 0;
            Step(w4, ButtonMask.Attack);
            Check(s.input.DirX == 1 && s.input.IsHeld(CommandKey.Attack), $"锁定前按住普攻并向右: dirX={s.input.DirX}");
            w4.InputLocked = true;
            Step(w4, ButtonMask.Attack, 2);
            Check(s.input.DirX == 0 && !s.input.IsHeld(CommandKey.Attack), $"锁定输入 (回合结束) 时视同松开全部按键、摇杆回中: dirX={s.input.DirX} 普攻={s.input.IsHeld(CommandKey.Attack)}");
            Frame.InputFrames[Slot].JoyStickDir = JoystickDir.None;
        }

        Console.WriteLine("== 9. 分身 (Actor)：大脑按脚本出招、可被命中、寿命到期播放消失动作 (无敌) 后回收；生命值归零立即回收 ==");
        {
            var actions = Actions("clone");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            t.hpMode = HpMode.Refill;
            var pool = new TestPool { Actions = actions };
            pool.Infos["clone"] = new SpawnData
            {
                id = "clone", type = "Actor", initialActionId = "idle", despawnActionId = "vanish", lifeTime = "0.5", hp = "30", brain = "Script",
                script = new[] { new CloneScriptStepData { frame = 1, hold = 1, buttons = new[] { "Attack" } } }
            }.ToRuntime();
            var w = World(p, t);
            w.Spawner = pool;

            Step(w, ButtonMask.Secret);
            Step(w, ButtonMask.None, 2);
            CharacterObj clone = w.Spawned.Count == 1 ? w.Spawned[0] : null;
            Check(clone != null && clone.Hittable && clone.InputSlot == -1 && F(clone.maxHp) == 30f, $"分身生成：可受击、无输入槽位、生命值 30: {clone?.EntityId} hp={(clone != null ? F(clone.maxHp) : -1)}");

            Step(w);
            Check(clone.action.CurrentAction.id == "idle", $"脚本第 0 帧不出招: {clone.action.CurrentAction.id}");
            Step(w);
            Check(clone.action.CurrentAction.id == "atk", $"脚本第 1 帧按下普攻 -> 出招: {clone.action.CurrentAction.id}");
            int hits = 0;
            for (int i = 0; i < 3; i++) { Step(w); foreach (var e in w.HitEvents) if (e.AttackerId == clone.EntityId && e.OwnerId == 1) hits++; }
            Check(hits == 1, $"分身命中目标，归属主人: {hits}");

            // 生成后第 15 帧 (0.5×30) 寿命到期 -> 消失动作 4 帧 -> 回收
            int lived = 5, despawnAt = -1;
            bool invincibleWhileVanishing = false;
            while (lived < 40 && despawnAt < 0)
            {
                Step(w);
                lived++;
                if (clone.action.CurrentAction.id == "vanish" && clone.IsInvincible) invincibleWhileVanishing = true;
                if (w.Despawned.Contains(clone.EntityId)) despawnAt = lived;
            }
            Check(invincibleWhileVanishing, "消失动作期间无敌");
            Check(despawnAt == 19, $"寿命 15 帧到期 + 消失动作 4 帧后回收: 第 {despawnAt} 帧 (预期 19)");

            var p2 = Player(actions);
            var t2 = Make(2, 2, "0.9", actions, false);
            t2.FacingLeft = true;
            var pool2 = new TestPool { Actions = actions };
            pool2.Infos["clone"] = pool.Infos["clone"];
            var w2 = World(p2, t2);
            w2.Spawner = pool2;
            Step(w2, ButtonMask.Secret);
            Step(w2, ButtonMask.None, 2);
            CharacterObj c2 = w2.Spawned[0];
            t2.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 });
            bool cloneHit = false;
            for (int i = 0; i < 4; i++) { Step(w2); foreach (var e in w2.HitEvents) if (e.TargetId == c2.EntityId) cloneHit = true; }
            Check(cloneHit && F(c2.Hp) == 20f, $"敌方普攻命中分身，分身扣血 30-10=20: 命中={cloneHit} hp={F(c2.Hp)}");
            c2.TakeDamage(FP.Parse("100"));
            Step(w2);
            Check(w2.Despawned.Contains(c2.EntityId), $"生命值归零当帧回收: 已死亡={c2.IsDead}");
        }

        Console.WriteLine("== 10. Buff：指令 addBuff 按 id 添加，不限层数，各层独立计时，到期移除并通知表现层停止特效 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            w.BuffTable["haste"] = new BuffData { id = "haste", duration = "0.2", effect = "fx_haste", anchor = "body" }.ToRuntime();
            p.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "buffact", priority = 200 });
            var log = new List<string>();
            for (int i = 1; i <= 12; i++)
            {
                Step(w);
                foreach (var v in w.ViewEvents)
                {
                    if (v.Type == ActionEventType.Buff || v.Type == ActionEventType.BuffRemoved) log.Add($"{i}:{v.Type}#{v.BuffSerial}:{v.Asset}");
                }
                if (i == 4) Check(p.Buffs.CountOf("haste") == 2, $"第 0 帧与第 2 帧各添加一层: {p.Buffs.CountOf("haste")} 层");
            }
            string s = string.Join(",", log);
            // 第 1 帧切入动作，第 0 帧指令在切入当帧添加 (持续 0.2×30=6 帧，第 7 帧到期)，第 4 帧处理动画第 2 帧时添加 (第 10 帧到期)
            Check(s == "1:Buff#1:fx_haste,4:Buff#2:fx_haste,7:BuffRemoved#1:fx_haste,10:BuffRemoved#2:fx_haste",
                $"添加与到期事件: {s}");
            Check(p.Buffs.Count == 0, $"全部到期后清空: {p.Buffs.Count}");
        }

        Console.WriteLine("== 11. 技能图标：未换段时用技能图标，skillStage 换段期间用指令配置的图标 (未配置沿用技能图标)；全部段图标在 Setup 时收集 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            Check(p.Skills.StageIcons.Count == 1 && p.Skills.StageIcons[0] == "atlas#stage2", $"收集 skillStage 图标: {string.Join(",", p.Skills.StageIcons)}");
            Check(p.Skills.GetIcon(multi) == "atlas#skill", $"未换段时用技能图标: {p.Skills.GetIcon(multi)}");
            Step(w, ButtonMask.Skill2);
            Check(p.Skills.GetIcon(multi) == "atlas#stage2", $"换段期间用段图标: {p.Skills.GetIcon(multi)}");
            Step(w, ButtonMask.None, 15);
            Check(p.Skills.GetWindow(multi) == 0 && p.Skills.GetIcon(multi) == "atlas#skill", $"超时恢复按键后回到技能图标: {p.Skills.GetIcon(multi)}");

            var noIcon = Replace(Actions("proj"), new ActionData
            {
                id = "m1", animKey = "m1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill2"),
                scripts = new[] { Stage("0.5") }
            });
            var q = Player(noIcon);
            var w2 = World(q);
            Step(w2, ButtonMask.Skill2);
            Check(q.Skills.GetWindow(multi) == 15 && q.Skills.GetIcon(multi) == "atlas#skill" && q.Skills.StageIcons.Count == 0,
                $"未配置段图标：换段期间沿用技能图标、不收集: 图标={q.Skills.GetIcon(multi)} 收集 {q.Skills.StageIcons.Count} 个");
        }

        Console.WriteLine("== 12. 技能派生 (技能划平 A)：技能释放中点普攻经取消窗口进入派生，派生沿用正在释放的技能 (回到 idle 才结束)，CD 从释放技能时开始计算；idle 下点普攻不会进入派生 ==");
        {
            var actions = Replace(Actions("proj"),
                new ActionData
                {
                    id = "s1", animKey = "s1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                    beCancelledTag = new[] { new BeCancelledTagData { frameRange = new FrameRange(4, 6), cancelTag = new[] { "s1_follow" } } }
                },
                new ActionData { id = "s1_follow", animKey = "atk", autoNextActionId = "idle", cancelTag = Tag("s1_follow"), commands = Cmd("Attack") });
            var p = Make(1, 1, "0", actions, true);
            p.InputSlot = Slot;
            var skills = new SkillController();
            skills.Setup(new[] { new SkillData { id = "s1", button = "Skill1", cooldown = "1", actionId = "s1" }.ToRuntime() }, actions, 4);
            p.Skills = skills;
            var w = World(p);

            Step(w, ButtonMask.Attack);
            Check(p.action.CurrentAction.id == "atk" && p.Skills.CastingSkill == -1, $"idle 下点普攻出普通攻击，派生标签只由技能窗口放行: {p.action.CurrentAction.id} 释放中={p.Skills.CastingSkill}");
            UntilIdle(w, p);

            // 第 0 帧放 s1，第 2 帧点普攻 (窗口 4~6 帧打开前)，靠预输入在窗口打开时进入派生
            Step(w, ButtonMask.Skill1);
            Step(w, ButtonMask.None);
            Step(w, ButtonMask.Attack);
            Step(w, ButtonMask.None, 3);
            Check(p.action.CurrentAction.id == "s1_follow" && p.Skills.CastingSkill == 0 && p.Skills.GetCooldown(0) == 25,
                $"窗口打开时接派生，仍算释放 s1，CD 从释放 s1 时起已倒计时 5 帧: 动作={p.action.CurrentAction.id} 释放中={p.Skills.CastingSkill} CD={p.Skills.GetCooldown(0)} (预期 25)");
            int n = UntilIdle(w, p);
            Check(p.action.CurrentAction.id == "idle" && p.Skills.CastingSkill == -1 && p.Skills.GetCooldown(0) == 25 - n,
                $"派生播完回到 idle 结束释放，CD 不重新计算: 用时 {n} 帧, 释放中={p.Skills.CastingSkill} CD={p.Skills.GetCooldown(0)} (预期 25-{n})");
        }

        Console.WriteLine("== 13. 动作指令 (scripts)：命中 / 帧 / 输入 / 离开触发，角色级变量跨动作保留，条件比较，受击锁定忽略跳转，resetHits ==");
        {
            ActionScriptData SetVar(int frame, string name, int value) => new ActionScriptData { frame = frame, op = "setVar", name = name, value = value };
            ActionScriptData On(string trigger, int from, int to, string op, string name, int value) =>
                new ActionScriptData { on = trigger, range = new FrameRange(from, to), op = op, name = name, value = value };
            ActionScriptData Goto(int frame, string action, ScriptConditionData[] cond = null) =>
                new ActionScriptData { frame = frame, cond = cond, op = "goto", action = action, priority = 50 };
            ScriptConditionData[] If(string name, string cmp, int value) => new[] { new ScriptConditionData { name = name, cmp = cmp, value = value } };
            AttackData Weak(int times) => new AttackData { attack = "1", hitStun = "0.3", canHitSameTarget = times, hitSameTargetDelay = "0" };
            var next = new ActionData { id = "s1_next", animKey = "vanish", autoNextActionId = "idle" };
            void Enter(CharacterObj c, string id) =>
                c.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = id, priority = 200 });

            Console.WriteLine("  -- (a) 跨动作命中分支：s1 (攻击 2~4 帧) 命中区间内 setVar hit 1 -> s1_land 第 3 帧 cond hit==1 goto s1_next，否则 autoNext idle");
            // reset：s1 第 0 帧 setVar hit 0 (清掉之前残留的值)；普攻任意帧命中也会写 hit 1 (制造残留)
            List<ActionInfo> Branch(int recFrom, int recTo, bool reset)
            {
                var s1Scripts = new List<ActionScriptData>();
                if (reset) s1Scripts.Add(SetVar(0, "hit", 0));
                s1Scripts.Add(On("hit", recFrom, recTo, "setVar", "hit", 1));
                return Replace(Actions("proj"),
                    new ActionData
                    {
                        id = "s1", animKey = "s1", autoNextActionId = "s1_land", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                        attackPhase = Box(2, 4), attacks = new[] { Weak(1) }, scripts = s1Scripts.ToArray()
                    },
                    new ActionData
                    {
                        id = "atk", animKey = "atk", autoNextActionId = "idle", cancelTag = Tag("attack"), commands = Cmd("Attack"),
                        attackPhase = Box(1, 3), attacks = new[] { Weak(1) }, scripts = new[] { On("hit", 0, 999, "setVar", "hit", 1) }
                    },
                    new ActionData { id = "s1_land", animKey = "standup", autoNextActionId = "idle", scripts = new[] { Goto(3, "s1_next", If("hit", "==", 1)) } },
                    next);
            }

            // 释放 s1 后记录经过的动作序列；staleHit：先用普攻打中目标 (hit=1 残留)，再把目标移走后释放 s1
            string Trace(List<ActionInfo> actions, bool targetInRange, bool staleHit = false)
            {
                var p = Player(actions);
                var t = Make(2, 2, "0.9", actions, false);
                var w = World(p, t);
                if (staleHit)
                {
                    Step(w, ButtonMask.Attack);
                    UntilIdle(w, p);
                }
                if (!targetInRange) t.motion.Reset(new FPVector3(FP.Parse("6"), FP.Zero, FP.Zero));
                Step(w, ButtonMask.Skill1);
                var seen = new List<string>();
                for (int i = 0; i < 40; i++)
                {
                    string id = p.action.CurrentAction.id;
                    if (seen.Count == 0 || seen[seen.Count - 1] != id) seen.Add(id);
                    Step(w);
                }
                return string.Join(">", seen);
            }

            string hit = Trace(Branch(0, 9, true), true);
            Check(hit == "s1>s1_land>s1_next>idle", $"s1 命中，s1_land 第 3 帧切到 s1_next: {hit}");
            string miss = Trace(Branch(0, 9, true), false);
            Check(miss == "s1>s1_land>idle", $"未命中：s1_land 播完回 idle: {miss}");
            string outside = Trace(Branch(6, 9, true), true);
            Check(outside == "s1>s1_land>idle", $"命中发生在 hit 区间 (6~9 帧) 之外不触发: {outside}");
            string cleared = Trace(Branch(0, 9, true), false, staleHit: true);
            Check(cleared == "s1>s1_land>idle", $"普攻留下的 hit=1 被 s1 第 0 帧 setVar hit 0 清掉: {cleared}");
            string stale = Trace(Branch(0, 9, false), false, staleHit: true);
            Check(stale == "s1>s1_land>s1_next>idle", $"不清零时变量跨动作保留 (对照组): {stale}");

            Console.WriteLine("  -- (b) hit 指令每个动作实例只执行一次 (多次命中也一样)，新的动作实例重新执行");
            {
                var actions = Replace(Actions("proj"), new ActionData
                {
                    id = "s1", animKey = "s1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                    attackPhase = Box(2, 4), attacks = new[] { Weak(3) }, scripts = new[] { On("hit", 0, 999, "addVar", "hits", 1) }
                });
                var p = Player(actions);
                var t = Make(2, 2, "0.9", actions, false);
                t.hpMode = HpMode.Refill;
                var w = World(p, t);
                int hits = 0;
                Step(w, ButtonMask.Skill1);
                for (int i = 0; i < 10; i++) { Step(w); hits += w.HitEvents.Count; }
                Check(hits == 3 && p.action.GetVar("hits") == 1, $"s1 命中 3 次，hit 指令只执行 1 次: 命中 {hits} 次, hits={p.action.GetVar("hits")}");
                Step(w, ButtonMask.None, 40);
                Step(w, ButtonMask.Skill1);
                Step(w, ButtonMask.None, 10);
                Check(p.action.GetVar("hits") == 2, $"再次释放 s1 (新实例) 命中后再执行一次: hits={p.action.GetVar("hits")}");
            }

            Console.WriteLine("  -- (c) 进入帧 (第 0 帧) 的指令在切入的同一逻辑帧执行，之后的帧随动画推进执行");
            {
                var actions = Replace(Actions("proj"), new ActionData
                {
                    id = "v0", animKey = "s1", autoNextActionId = "idle", scripts = new[] { SetVar(0, "a", 1), SetVar(3, "a", 2) }
                });
                var p = Player(actions);
                var w = World(p);
                Enter(p, "v0");
                Step(w);
                Check(p.action.CurrentAction.id == "v0" && p.action.GetVar("a") == 1, $"切入 v0 的当帧第 0 帧指令已执行: 动作={p.action.CurrentAction.id} a={p.action.GetVar("a")}");
                Step(w, ButtonMask.None, 3);
                Check(p.action.GetVar("a") == 1, $"处理第 0~2 帧：第 3 帧指令尚未执行: a={p.action.GetVar("a")}");
                Step(w);
                Check(p.action.GetVar("a") == 2, $"处理到第 3 帧时执行: a={p.action.GetVar("a")}");
                Check(p.action.GetVar("unknown") == 0, $"没有出现在任何指令中的变量读出为 0: {p.action.GetVar("unknown")}");
            }

            Console.WriteLine("  -- (d) 受击锁定期间 goto 被忽略 (同一动作的其他指令照常执行)");
            {
                var actions = Replace(Actions("proj"),
                    new ActionData { id = "hurt", animKey = "hurt", scripts = new[] { SetVar(1, "hurtSeen", 1), Goto(1, "s1_next") } },
                    next);
                var p = Player(actions);
                var t = Make(2, 2, "0.9", actions, false);
                t.FacingLeft = true;
                var w = World(p, t);
                Enter(t, "atk");
                Step(w, ButtonMask.None, 3);
                Check(p.Hurt == HurtState.Stun && p.action.CurrentAction.id == "hurt", $"被普攻命中进入僵直: {p.Hurt} {p.action.CurrentAction.id}");
                Step(w, ButtonMask.None, 3);
                Check(p.action.CurrentAction.id == "hurt" && p.action.GetVar("hurtSeen") == 1,
                    $"hurt 第 1 帧 setVar 已执行，同帧 goto s1_next 被忽略: 动作={p.action.CurrentAction.id} hurtSeen={p.action.GetVar("hurtSeen")}");
            }

            Console.WriteLine("  -- (e) input 指令：动画处于区间 (4~6 帧) 内按下按键执行一次，区间外按下不执行");
            {
                var actions = Replace(Actions("proj"), new ActionData
                {
                    id = "inp", animKey = "s1", autoNextActionId = "idle",
                    scripts = new[] { new ActionScriptData { on = "input", range = new FrameRange(4, 6), keys = new[] { "Attack" }, op = "addVar", name = "inp", value = 1 } }
                });
                var p = Player(actions);
                var w = World(p);
                Enter(p, "inp");
                Step(w);
                Step(w, ButtonMask.None, 7);
                Step(w, ButtonMask.Attack);
                Step(w, ButtonMask.None);
                Step(w, ButtonMask.Attack);
                Check(p.action.CurrentAction.id == "idle" && p.action.GetVar("inp") == 0, $"第 7、9 帧 (区间外) 按下不执行: 动作={p.action.CurrentAction.id} inp={p.action.GetVar("inp")}");
                Step(w);
                Enter(p, "inp");
                Step(w);
                Step(w, ButtonMask.None, 4);
                Step(w, ButtonMask.Attack);
                Check(p.action.CurrentAction.id == "inp" && p.action.GetVar("inp") == 1, $"第 4 帧 (区间内) 按下执行: inp={p.action.GetVar("inp")}");
                Step(w, ButtonMask.None);
                Step(w, ButtonMask.Attack);
                UntilIdle(w, p);
                Check(p.action.GetVar("inp") == 1, $"区间内第 6 帧再次按下不重复执行: inp={p.action.GetVar("inp")}");
            }

            Console.WriteLine("  -- (f) exit 指令：离开动作时执行 (播完、受击都算)，其中的 goto 被忽略");
            {
                var actions = Replace(Actions("proj"),
                    new ActionData
                    {
                        id = "ex", animKey = "s1", autoNextActionId = "idle",
                        scripts = new[]
                        {
                            new ActionScriptData { on = "exit", op = "addVar", name = "exits", value = 1 },
                            new ActionScriptData { on = "exit", op = "goto", action = "s1_next", priority = 500 }
                        }
                    },
                    next);
                var p = Player(actions);
                var t = Make(2, 2, "0.9", actions, false);
                t.FacingLeft = true;
                t.motion.Reset(new FPVector3(FP.Parse("6"), FP.Zero, FP.Zero));
                var w = World(p, t);
                Enter(p, "ex");
                Step(w);
                Step(w, ButtonMask.None, 9);
                Check(p.action.CurrentAction.id == "ex" && p.action.GetVar("exits") == 0, $"播放中不执行: 动作={p.action.CurrentAction.id} exits={p.action.GetVar("exits")}");
                Step(w);
                Check(p.action.CurrentAction.id == "idle" && p.action.GetVar("exits") == 1, $"播完离开时执行: 动作={p.action.CurrentAction.id} exits={p.action.GetVar("exits")}");
                Step(w, ButtonMask.None, 2);
                Check(p.action.CurrentAction.id == "idle", $"exit 中的 goto 被忽略: {p.action.CurrentAction.id}");

                t.motion.Reset(new FPVector3(FP.Parse("0.9"), FP.Zero, FP.Zero));
                Enter(p, "ex");
                Enter(t, "atk");
                Step(w);
                Step(w, ButtonMask.None, 2);
                Check(p.action.CurrentAction.id == "hurt" && p.action.GetVar("exits") == 2, $"被命中离开时执行: 动作={p.action.CurrentAction.id} exits={p.action.GetVar("exits")}");
            }

            Console.WriteLine("  -- (g) addVar 与条件比较符");
            {
                var actions = Replace(Actions("proj"), new ActionData
                {
                    id = "cnt", animKey = "s1", autoNextActionId = "idle",
                    scripts = new[]
                    {
                        SetVar(0, "lt", 0), SetVar(0, "le", 0), SetVar(0, "gt", 0), SetVar(0, "ge", 0), SetVar(0, "ne", 0), SetVar(0, "eq", 0),
                        new ActionScriptData { frame = 0, op = "addVar", name = "n", value = 1 },
                        new ActionScriptData { frame = 1, cond = If("n", "<", 2), op = "setVar", name = "lt", value = 1 },
                        new ActionScriptData { frame = 1, cond = If("n", "<=", 1), op = "setVar", name = "le", value = 1 },
                        new ActionScriptData { frame = 1, cond = If("n", ">", 1), op = "setVar", name = "gt", value = 1 },
                        new ActionScriptData { frame = 1, cond = If("n", ">=", 2), op = "setVar", name = "ge", value = 1 },
                        new ActionScriptData { frame = 1, cond = If("n", "!=", 1), op = "setVar", name = "ne", value = 1 },
                        new ActionScriptData { frame = 1, cond = If("n", null, 1), op = "setVar", name = "eq", value = 1 },
                    }
                });
                var p = Player(actions);
                var w = World(p);
                string Vars() => $"n={p.action.GetVar("n")} lt={p.action.GetVar("lt")} le={p.action.GetVar("le")} gt={p.action.GetVar("gt")} ge={p.action.GetVar("ge")} ne={p.action.GetVar("ne")} eq={p.action.GetVar("eq")}";
                Enter(p, "cnt");
                Step(w, ButtonMask.None, 3);
                Check(Vars() == "n=1 lt=1 le=1 gt=0 ge=0 ne=0 eq=1", $"第 1 次进入 n=1：< <= == (留空) 成立，> >= != 不成立: {Vars()}");
                UntilIdle(w, p);
                Enter(p, "cnt");
                Step(w, ButtonMask.None, 3);
                Check(Vars() == "n=2 lt=0 le=0 gt=1 ge=1 ne=1 eq=0", $"第 2 次进入 addVar 累加到 n=2：> >= != 成立，< <= == 不成立: {Vars()}");
            }

            Console.WriteLine("  -- (h) resetHits：清空命中记录，同一动作内已命中的目标可再次命中");
            {
                int Hits(bool reset)
                {
                    var actions = Replace(Actions("proj"), new ActionData
                    {
                        id = "rh", animKey = "s1", autoNextActionId = "idle", attackPhase = Box(1, 6), attacks = new[] { Weak(1) },
                        scripts = reset ? new[] { new ActionScriptData { frame = 4, op = "resetHits" } } : null
                    });
                    var p = Player(actions);
                    var t = Make(2, 2, "0.9", actions, false);
                    t.hpMode = HpMode.Refill;
                    var w = World(p, t);
                    Enter(p, "rh");
                    int n = 0;
                    for (int i = 0; i < 11; i++) { Step(w); n += w.HitEvents.Count; }
                    return n;
                }
                int without = Hits(false), with = Hits(true);
                Check(without == 1, $"canHitSameTarget=1：同一动作内只命中 1 次 (对照组): {without}");
                Check(with == 2, $"第 4 帧 resetHits 后再次命中: {with}");
            }
        }

        Console.WriteLine("== 14. Buff 当计时标记：指令 addBuff / removeBuff (切入当帧生效、跨动作保留、可同时存在多个)，配置了 button 的 Buff 在该按键显示剩余最多的一层，条件 buff 读取剩余帧数，叠层上限只刷新时间，重置战斗状态清空 ==");
        {
            ActionScriptData Add(string buff) => new ActionScriptData { frame = 0, op = "addBuff", buff = buff };
            var actions = Replace(Actions("proj"),
                new ActionData
                {
                    id = "s1", animKey = "s1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                    scripts = new[] { Add("atkEx"), Add("stance"), Add("hidden") }
                },
                new ActionData
                {
                    id = "atk", animKey = "atk", autoNextActionId = "idle", cancelTag = Tag("attack"), commands = Cmd("Attack"),
                    scripts = new[]
                    {
                        new ActionScriptData
                        {
                            frame = 0, op = "goto", action = "atk_ex", priority = 10,
                            cond = new[] { new ScriptConditionData { buff = "atkEx", cmp = ">", value = 0 } }
                        }
                    }
                },
                new ActionData { id = "atk_ex", animKey = "atk", autoNextActionId = "idle", scripts = new[] { new ActionScriptData { frame = 0, op = "removeBuff", buff = "atkEx" } } });
            var p = Player(actions);
            var w = World(p);
            w.BuffTable["atkEx"] = new BuffData { id = "atkEx", duration = "0.5", button = "Attack" }.ToRuntime();
            w.BuffTable["stance"] = new BuffData { id = "stance", duration = "1", button = "Attack", maxStack = 1 }.ToRuntime();
            w.BuffTable["hidden"] = new BuffData { id = "hidden", duration = "0.2" }.ToRuntime();
            BuffController buffs = p.Buffs;
            int ButtonRemaining(CommandKey key) => buffs.TryGetButtonBuff(key, out int remaining, out _) ? remaining : 0;

            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "s1" && buffs.GetRemaining("atkEx") == 15 && buffs.GetRemaining("stance") == 30 && buffs.GetRemaining("hidden") == 6,
                $"进入 s1 当帧添加三个 Buff 15 / 30 / 6 帧: atkEx={buffs.GetRemaining("atkEx")} stance={buffs.GetRemaining("stance")} hidden={buffs.GetRemaining("hidden")}");
            Check(buffs.TryGetButtonBuff(CommandKey.Attack, out int r0, out int t0) && r0 == 30 && t0 == 30 && ButtonRemaining(CommandKey.Skill1) == 0,
                $"Attack 上配置了两个 Buff 时显示剩余最多的 stance，未配置的按键没有: Attack={r0}/{t0} Skill1={ButtonRemaining(CommandKey.Skill1)}");
            int n = UntilIdle(w, p);
            Check(buffs.GetRemaining("atkEx") == 15 - n && buffs.CountOf("hidden") == 0,
                $"回到 idle 后继续倒数 (跨动作保留)，到期的移除: atkEx={buffs.GetRemaining("atkEx")} (预期 15-{n}) hidden 层数={buffs.CountOf("hidden")}");

            Step(w, ButtonMask.Attack);
            Step(w);
            Check(p.action.CurrentAction.id == "atk_ex" && buffs.CountOf("atkEx") == 0 && buffs.GetRemaining("stance") > 0,
                $"有 atkEx 时按普攻：条件 buff atkEx > 0 成立跳到派生，派生 removeBuff 只移除 atkEx: 动作={p.action.CurrentAction.id} atkEx={buffs.CountOf("atkEx")} stance={buffs.GetRemaining("stance")}");
            UntilIdle(w, p);
            Step(w, ButtonMask.Attack);
            Step(w);
            Check(p.action.CurrentAction.id == "atk", $"没有 atkEx 时条件不成立，普攻不派生: {p.action.CurrentAction.id}");
            Step(w, ButtonMask.None, 30);
            Check(ButtonRemaining(CommandKey.Attack) == 0 && buffs.Count == 0, $"全部到期后按键上不再显示: Attack={ButtonRemaining(CommandKey.Attack)} 层数={buffs.Count}");

            Step(w, ButtonMask.Skill1);
            Step(w, ButtonMask.None, 10);
            w.AddBuff(p, w.BuffTable["stance"]);
            Check(buffs.CountOf("stance") == 1 && buffs.GetRemaining("stance") == 30 && buffs.CountOf("atkEx") == 1,
                $"stance 叠层上限 1：再添加只刷新剩余时间；atkEx 不限层数: stance 层数={buffs.CountOf("stance")} 剩余={buffs.GetRemaining("stance")} atkEx 层数={buffs.CountOf("atkEx")}");
            p.ResetCombatState();
            Check(buffs.Count == 0 && ButtonRemaining(CommandKey.Attack) == 0, $"重置战斗状态清空 Buff: {buffs.Count}");
        }

        Console.WriteLine("== 15. Buff 属性与秘卷：伤害加成 / 暴击率加成按层累加，hit 指令 toTarget 给被打中的目标加 Buff，技能配置 buff 时释放即给自己加 ==");
        {
            AttackData Hit10() => new AttackData { attack = "10", hitStun = "0.3", canHitSameTarget = 1, hitSameTargetDelay = "0" };
            void Enter(CharacterObj c, string id) =>
                c.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = id, priority = 200 });
            var actions = Replace(Actions("proj"),
                new ActionData
                {
                    id = "hit10", animKey = "atk", autoNextActionId = "idle", attackPhase = Box(1, 3), attacks = new[] { Hit10() },
                    scripts = new[] { new ActionScriptData { on = "hit", range = new FrameRange(0, 999), op = "addBuff", buff = "seal", toTarget = true } }
                });

            FP HitOnce(CharacterObj p, CharacterObj t, BattleWorld w, out bool critical)
            {
                critical = false;
                Enter(p, "hit10");
                for (int i = 0; i < 8; i++)
                {
                    Step(w);
                    if (w.HitEvents.Count > 0)
                    {
                        critical = w.HitEvents[0].Critical;
                        FP damage = w.HitEvents[0].Damage;
                        UntilIdle(w, p);
                        return damage;
                    }
                }
                return FP.Zero;
            }

            var a = Player(actions);
            var target = Make(2, 2, "0.9", actions, false);
            target.hpMode = HpMode.Refill;
            var wa = World(a, target);
            wa.BuffTable["power"] = new BuffData { id = "power", duration = "10", damageRate = "0.5" }.ToRuntime();
            wa.BuffTable["fury"] = new BuffData { id = "fury", duration = "10", critRate = "1" }.ToRuntime();
            wa.BuffTable["seal"] = new BuffData { id = "seal", duration = "3" }.ToRuntime();
            a.critRate = FP.Zero;

            FP baseDamage = HitOnce(a, target, wa, out bool crit0);
            Check(baseDamage == FP.Parse("10") && !crit0, $"无 Buff：伤害 10、暴击率 0 不暴击: {baseDamage} 暴击={crit0}");
            Check(target.Buffs.CountOf("seal") == 1 && a.Buffs.CountOf("seal") == 0, $"hit 指令 toTarget：Buff 加在被打中的目标身上: 目标={target.Buffs.CountOf("seal")} 自己={a.Buffs.CountOf("seal")}");

            wa.AddBuff(a, wa.BuffTable["power"]);
            FP oneLayer = HitOnce(a, target, wa, out _);
            wa.AddBuff(a, wa.BuffTable["power"]);
            FP twoLayers = HitOnce(a, target, wa, out _);
            Check(oneLayer == FP.Parse("15") && twoLayers == FP.Parse("20"), $"伤害加成 0.5 每层累加：1 层 15、2 层 20: {oneLayer} / {twoLayers}");

            wa.AddBuff(a, wa.BuffTable["fury"]);
            FP critDamage = HitOnce(a, target, wa, out bool crit1);
            Check(crit1 && critDamage == FP.Parse("20") * a.critMultiplier, $"暴击率加成 1：必定暴击，伤害 = 20 × 暴击倍率 {a.critMultiplier}: {critDamage} 暴击={crit1}");

            // 秘卷：技能配置 buff，释放 (进入入口动作) 时给自己加
            var scroll = Make(3, 1, "0", Actions("proj"), true);
            scroll.InputSlot = Slot;
            var scrollSkills = new SkillController();
            scrollSkills.Setup(new[] { new SkillData { id = "secret", button = "Skill1", cooldown = "5", actionId = "s1", buff = "power" }.ToRuntime() }, Actions("proj"), 4);
            scroll.Skills = scrollSkills;
            var ws = World(scroll);
            ws.BuffTable["power"] = wa.BuffTable["power"];
            Step(ws, ButtonMask.Skill1);
            Check(scroll.action.CurrentAction.id == "s1" && scroll.Buffs.CountOf("power") == 1 && scroll.Buffs.GetRemaining("power") == 300 && scroll.Skills.GetCooldown(0) == 150,
                $"释放带 buff 的技能：当帧给自己加 power (10×30=300 帧) 并开始 CD 5×30=150: 动作={scroll.action.CurrentAction.id} 层数={scroll.Buffs.CountOf("power")} 剩余={scroll.Buffs.GetRemaining("power")} CD={scroll.Skills.GetCooldown(0)}");
        }

        Console.WriteLine(_fail == 0 ? "\n全部通过" : $"\n失败 {_fail} 项");
        return _fail == 0 ? 0 : 1;
    }

    static float F(FP v) => v.AsFloat();
}
