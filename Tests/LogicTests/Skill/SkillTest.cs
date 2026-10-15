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
            new ActionData { id = "hurt_1", animKey = "hurt" }, new ActionData { id = "hurt_2", animKey = "hurt" },
            new ActionData { id = "hurt_toair", animKey = "hurt" }, new ActionData { id = "hurt_inair", animKey = "hurt" },
            new ActionData { id = "hurt_fall", animKey = "hurt" }, new ActionData { id = "lie", animKey = "lie" },
            new ActionData { id = "standup", animKey = "standup" },
            new ActionData
            {
                id = "s1", animKey = "s1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                attackPhase = Box(2, 4), attacks = new[] { Hit("10", 3) }
            },
            new ActionData { id = "m1", animKey = "m1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill2") },
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
                    new ActionEventData { frame = 3, type = "Camera", asset = CameraEffects.ImpactHeavy }
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
                events = new[]
                {
                    new ActionEventData { frame = 0, type = "Buff", asset = "haste" },
                    new ActionEventData { frame = 2, type = "Buff", asset = "haste" }
                }
            },
        };

        var list = new List<ActionInfo>();
        foreach (ActionData d in data) list.Add(d.ToRuntime());
        return list;
    }

    static SkillInfo[] Skills()
    {
        SkillStageData Stage(string id, string window = null, string refund = null) => new SkillStageData { actionId = id, stageDuration = window, cdReturn = refund };
        var data = new[]
        {
            new SkillData { id = "s1", button = "Skill1", cooldown = "1", stages = new[] { Stage("s1") } },
            new SkillData { id = "multi", button = "Skill2", cooldown = "2", stages = new[] { Stage("m1", "0.5", "0.5"), Stage("m2") } },
            new SkillData { id = "sub", button = "Substitution", cooldown = "1", requireHurt = true, stages = new[] { Stage("sub") } },
            new SkillData { id = "summon", button = "Secret", cooldown = "1", stages = new[] { Stage("summon") } },
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
        skills.Setup(Skills(), 4);
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

        Console.WriteLine("== 1. 单段技能：释放完 (离开技能动作) 后开始计算完整 CD，CD 中按键无效，CD 结束可再次释放 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "s1" && p.Skills.CastingSkill == s1 && p.Skills.GetCooldown(s1) == 0,
                $"按下即进入 s1，释放中不计 CD: 动作={p.action.CurrentAction.id} CD={p.Skills.GetCooldown(s1)}");
            int n = UntilIdle(w, p);
            Check(n == 10 && p.Skills.GetCooldown(s1) == 30, $"s1 播完 (预期 10 帧) 回到 idle 当帧开始 CD，预期 1 秒×30=30: 用时 {n} 帧, CD={p.Skills.GetCooldown(s1)}");
            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "idle" && p.Skills.GetCooldown(s1) == 29, $"CD 中按键无效: 动作={p.action.CurrentAction.id}, CD={p.Skills.GetCooldown(s1)} (预期 29)");
            Step(w, ButtonMask.None, 29);
            Check(p.Skills.GetCooldown(s1) == 0, $"再过 29 帧 CD 归零: {p.Skills.GetCooldown(s1)}");
            Step(w, ButtonMask.Skill1);
            Check(p.action.CurrentAction.id == "s1", $"CD 结束后再次释放: {p.action.CurrentAction.id}");
        }

        Console.WriteLine("== 2. 多段技能：段间时限内接下一段，最后一段释放完后计算完整 CD；超时未接则返还 cdReturn ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            Step(w, ButtonMask.Skill2);
            UntilIdle(w, p);
            Check(p.Skills.GetNextStage(multi) == 1 && p.Skills.GetWindow(multi) == 15 && p.Skills.GetCooldown(multi) == 0,
                $"第 1 段放完：下一段=1、接段时限 0.5×30=15、CD 未开始: 段={p.Skills.GetNextStage(multi)} 时限={p.Skills.GetWindow(multi)} CD={p.Skills.GetCooldown(multi)}");
            Step(w, ButtonMask.Skill2);
            Check(p.action.CurrentAction.id == "m2", $"时限内同一按键接第 2 段 (第 1 段入口被准入拦截): {p.action.CurrentAction.id}");
            Check(p.Skills.GetCooldown(multi) == 0, $"第 2 段释放中仍不计 CD: {p.Skills.GetCooldown(multi)}");
            UntilIdle(w, p);
            Check(p.Skills.GetCooldown(multi) == 60 && p.Skills.GetNextStage(multi) == 0,
                $"最后一段释放完开始完整 CD 2×30=60 并回到第 1 段: CD={p.Skills.GetCooldown(multi)} 段={p.Skills.GetNextStage(multi)}");

            var q = Player(Actions("proj"));
            var w2 = World(q);
            Step(w2, ButtonMask.Skill2);
            UntilIdle(w2, q);
            Step(w2, ButtonMask.None, 14);
            Check(q.Skills.GetWindow(multi) == 1 && q.Skills.GetCooldown(multi) == 0, $"时限最后一帧前仍可接段: 时限={q.Skills.GetWindow(multi)} CD={q.Skills.GetCooldown(multi)}");
            Step(w2);
            Check(q.Skills.GetCooldown(multi) == 45 && q.Skills.GetNextStage(multi) == 0,
                $"超时：CD = 60 - 返还 0.5×30=15 = 45，回到第 1 段: CD={q.Skills.GetCooldown(multi)} 段={q.Skills.GetNextStage(multi)}");
            Step(w2, ButtonMask.Skill2);
            Check(q.action.CurrentAction.id == "idle", $"超时后 CD 中两段都不能释放: {q.action.CurrentAction.id}");
        }

        Console.WriteLine("== 3. 查克拉：技能命中获得 1 颗，同一轮多次命中只获得 1 颗；普攻不获得；CD 刷新后可再次获得 ==");
        {
            var actions = Actions("proj");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            t.infiniteHp = true;
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
            t.infiniteHp = true;
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

            t.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 }, ForceDirection.Forward);
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
                t.infiniteHp = true;
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
            Step(w, ButtonMask.Ultimate);
            for (int i = 0; i < 10; i++)
            {
                Step(w);
                foreach (var v in w.ViewEvents)
                {
                    seen.Add($"{i}:{v.Type}:{v.Asset}");
                    if (v.Asset == "fx_a") serialAtFx = v.ActionSerial;
                }
            }
            string s = string.Join(",", seen);
            Check(s == "0:Effect:fx_a,3:Camera:impact_heavy", $"第 0 帧特效、第 3 帧镜头各触发一次: {s}");
            Check(serialAtFx >= 0 && serialAtFx != p.action.ActionSerial, $"事件携带动作序号，动作结束后序号已改变 (表现层据此停止特效): 事件={serialAtFx} 当前={p.action.ActionSerial}");

            p.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "fxact", priority = 200, fromFrame = 2 }, ForceDirection.Forward);
            Step(w);
            seen.Clear();
            for (int i = 0; i < 8; i++) { Step(w); foreach (var v in w.ViewEvents) seen.Add(v.Asset); }
            Check(string.Join(",", seen) == "impact_heavy", $"从第 2 帧切入：第 0 帧事件不补发: {string.Join(",", seen)}");
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

            t.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 }, ForceDirection.Forward);
            Step(w, ButtonMask.None, 3);
            Check(p.Hurt == HurtState.Stun && p.action.CurrentAction.id == "hurt_1", $"被普攻命中进入僵直: {p.Hurt} {p.action.CurrentAction.id}");
            Step(w, ButtonMask.Substitution);
            Check(p.action.CurrentAction.id == "sub" && p.Hurt == HurtState.Standup && p.IsInvincible && p.StunTicks == 0,
                $"僵直中按替身：进入替身动作、起身无敌: {p.action.CurrentAction.id} {p.Hurt} 无敌={p.IsInvincible}");

            t.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 }, ForceDirection.Forward);
            int hits = 0, n = 0;
            while (p.action.CurrentAction.id == "sub" && n < 20) { Step(w); hits += w.HitEvents.Count; n++; }
            Check(hits == 0, $"替身期间不受命中: {hits}");
            Check(n == 5 && p.Hurt == HurtState.None && p.InvincibleTicks == 15 && p.action.CurrentAction.id == "idle",
                $"替身动作 5 帧播完回到 idle，追加 0.5×30=15 帧无敌: 用时 {n}, {p.Hurt}, 无敌 {p.InvincibleTicks}");
            Check(p.Skills.GetCooldown(sub) == 30, $"替身释放完开始 CD 1×30=30: {p.Skills.GetCooldown(sub)}");

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

        Console.WriteLine("== 9. 分身 (Actor)：大脑按脚本出招、可被命中、寿命到期播放消失动作 (无敌) 后回收；生命值归零立即回收 ==");
        {
            var actions = Actions("clone");
            var p = Player(actions);
            var t = Make(2, 2, "0.9", actions, false);
            t.infiniteHp = true;
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
            t2.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "atk", priority = 200 }, ForceDirection.Forward);
            bool cloneHit = false;
            for (int i = 0; i < 4; i++) { Step(w2); foreach (var e in w2.HitEvents) if (e.TargetId == c2.EntityId) cloneHit = true; }
            Check(cloneHit && F(c2.Hp) == 20f, $"敌方普攻命中分身，分身扣血 30-10=20: 命中={cloneHit} hp={F(c2.Hp)}");
            c2.TakeDamage(FP.Parse("100"));
            Step(w2);
            Check(w2.Despawned.Contains(c2.EntityId), $"生命值归零当帧回收: 已死亡={c2.IsDead}");
        }

        Console.WriteLine("== 10. Buff：动作事件按 id 添加，不限层数，各层独立计时，到期移除并通知表现层停止特效 ==");
        {
            var p = Player(Actions("proj"));
            var w = World(p);
            w.BuffTable["haste"] = new BuffData { id = "haste", duration = "0.2", effect = "fx_haste", anchor = "body" }.ToRuntime();
            p.action.PreorderActionByActionChangeInfo(new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "buffact", priority = 200 }, ForceDirection.Forward);
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
            // 第 1 帧切入动作，第 2 帧处理动画第 0 帧添加 (持续 0.2×30=6 帧，第 8 帧到期)，第 4 帧处理第 2 帧添加 (第 10 帧到期)
            Check(s == "2:Buff#1:fx_haste,4:Buff#2:fx_haste,8:BuffRemoved#1:fx_haste,10:BuffRemoved#2:fx_haste",
                $"添加与到期事件: {s}");
            Check(p.Buffs.Count == 0, $"全部到期后清空: {p.Buffs.Count}");
        }

        Console.WriteLine("== 11. 技能图标配置：各段图标优先，未配置的段沿用技能图标 ==");
        {
            var skill = new SkillData
            {
                id = "multi", icon = "atlas#skill",
                stages = new[] { new SkillStageData { actionId = "m1" }, new SkillStageData { actionId = "m2", icon = "atlas#stage2" } }
            }.ToRuntime();
            Check(skill.IconOfStage(0) == "atlas#skill" && skill.IconOfStage(1) == "atlas#stage2",
                $"第 1 段沿用技能图标、第 2 段用自己的图标: {skill.IconOfStage(0)} / {skill.IconOfStage(1)}");
        }

        Console.WriteLine("== 12. 技能派生 (技能划平 A)：技能释放中点普攻接派生 (followActions)，派生期间不计 CD，回到 idle 才开始 CD；idle 下点普攻不会进入派生 ==");
        {
            var actions = Actions("proj");
            actions[actions.FindIndex(a => a.id == "s1")] = new ActionData
            {
                id = "s1", animKey = "s1", autoNextActionId = "idle", cancelTag = Tag("skill"), commands = Cmd("Skill1"),
                beCancelledTag = new[] { new BeCancelledTagData { frameRange = new FrameRange(4, 6), cancelTag = new[] { "s1_follow" } } }
            }.ToRuntime();
            actions.Add(new ActionData { id = "s1_follow", animKey = "atk", autoNextActionId = "idle", cancelTag = Tag("s1_follow"), commands = Cmd("Attack") }.ToRuntime());
            var p = Make(1, 1, "0", actions, true);
            p.InputSlot = Slot;
            var skills = new SkillController();
            var follow = new SkillData { id = "s1", button = "Skill1", cooldown = "1", stages = new[] { new SkillStageData { actionId = "s1", followActions = new[] { "s1_follow" } } } };
            skills.Setup(new[] { follow.ToRuntime() }, 4);
            p.Skills = skills;
            var w = World(p);

            Step(w, ButtonMask.Attack);
            Check(p.action.CurrentAction.id == "atk", $"idle 下点普攻出普通攻击，派生标签只由技能窗口放行: {p.action.CurrentAction.id}");
            UntilIdle(w, p);

            // 第 0 帧放 s1，第 2 帧点普攻 (窗口 4~6 帧打开前)，靠预输入在窗口打开时进入派生
            Step(w, ButtonMask.Skill1);
            Step(w, ButtonMask.None);
            Step(w, ButtonMask.Attack);
            Step(w, ButtonMask.None, 3);
            Check(p.action.CurrentAction.id == "s1_follow" && p.Skills.CastingSkill == 0 && p.Skills.GetCooldown(0) == 0,
                $"窗口打开时接派生，仍算释放 s1 且不计 CD: 动作={p.action.CurrentAction.id} 释放中={p.Skills.CastingSkill} CD={p.Skills.GetCooldown(0)}");
            int n = UntilIdle(w, p);
            Check(p.action.CurrentAction.id == "idle" && p.Skills.GetCooldown(0) == 30, $"派生播完回到 idle 当帧开始完整 CD (预期 30): 用时 {n} 帧, CD={p.Skills.GetCooldown(0)}");
        }

        Console.WriteLine(_fail == 0 ? "\n全部通过" : $"\n失败 {_fail} 项");
        return _fail == 0 ? 0 : 1;
    }

    static float F(FP v) => v.AsFloat();
}
