using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using KiHan.Logic;
using UnityEngine;

static class Program
{
    static AnimationFrameData Anim(int frames, LogicBox? hit, LogicBox? hurt)
    {
        var lib = new CharacterSpriteLibrary();
        var fd = new SpriteFrameData();
        if (hit.HasValue) fd.HitBoxes.Add(hit.Value);
        if (hurt.HasValue) fd.HurtBoxes.Add(hurt.Value);
        lib.AllFrames.Add(fd);
        var a = new AnimationFrameData { Library = lib };
        for (int i = 0; i < frames; i++) a.Steps.Add(new AnimationFrameData.AnimStep { FrameIndex = 0, Duration = 1 });
        return a;
    }

    static bool OpenWindow = true;
    static CharacterObj Make(int id, int team, string startAction, AttackData atkData)
    {
        var hurtBox = new LogicBox(new Vector2Int(0, 80), new Vector2Int(60, 160), 40);
        var anim = new FrameAnimator();
        anim.RegisterAnim("attack", Anim(40, new LogicBox(new Vector2Int(60, 80), new Vector2Int(80, 60), 40), hurtBox));
        anim.RegisterAnim("hurt_1", Anim(3, null, hurtBox));
        anim.RegisterAnim("hurt_air", Anim(2, null, hurtBox));
        anim.RegisterAnim("hurt_land", Anim(3, null, hurtBox));
        anim.RegisterAnim("idle", Anim(4, null, hurtBox));

        var attack = new ActionInfo { id = "attack", animKey = "attack", autoNextActionId = "attack",
            cancelTag = new[] { new CancelTag { tag = "attack" } },
            attackPhase = new[] { new AttackBoxTurnOnInfo { frameRanges = new[] { new FrameRange(0, 999) }, attackPhase = 0 } },
            attacks = new[] { atkData.ToRuntime() } };
        // 受击动作故意开放 "attack" 取消窗口：若未锁定，受击者会立刻出招
        var hurt1 = new ActionInfo { id = "hurt_1", animKey = "hurt_1", catalog = "Hurt", autoNextActionId = "idle",
            beCancelledTag = OpenWindow ? new[] { new BeCancelledTag { range = new Vector2Int(0, 999), cancelTag = new[] { "attack" } } } : null };
        var hurtAir = new ActionInfo { id = "hurt_air", animKey = "hurt_air", autoNextActionId = "hurt_air", keepPlayingAnim = true,
            beCancelledTag = new[] { new BeCancelledTag { range = new Vector2Int(0, 999), cancelTag = new[] { "attack" } } },
            onLand = new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "hurt_land", priority = 100 } };
        var hurtLand = new ActionInfo { id = "hurt_land", animKey = "hurt_land", autoNextActionId = "idle" };
        var idle = new ActionInfo { id = "idle", animKey = "idle", autoNextActionId = "idle", keepPlayingAnim = true };

        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = id, TeamId = team };
        ac.SetAllActions(new List<ActionInfo> { attack, hurt1, hurtAir, hurtLand, idle }, startAction);
        cha.motion.Reset(new FPVector3(id == 1 ? FP.Zero : FP.Parse("0.9"), FP.Zero, FP.Zero));
        return cha;
    }

    static void Run(string title, AttackData atk, int frames)
    {
        Console.WriteLine(title);
        var world = new BattleWorld(new MapLogic());
        var a = Make(1, 1, "attack", atk);
        var t = Make(2, 2, "hurt_land", atk); // 受击者先播一个不可取消的动作，随后回到 idle
        world.AddCharacter(a); world.AddCharacter(t);
        var frame = new RoomFrame();
        for (uint f = 1; f <= frames; f++)
        {
            frame.FrameId = f;
            world.Tick(frame);
            Console.WriteLine($"  f{f,2} 攻击者={a.action.CurrentAction.id,-6}@{a.action.anim.ElapsedFrames,2} | 受击者={t.action.CurrentAction.id,-9}@{t.action.anim.ElapsedFrames} 状态={t.Hurt,-6} 僵直剩余={t.StunTicks} 锁定={t.action.ControlLocked,-5} z={t.motion.Position.z.AsFloat():F2} {(world.HitEvents.Count > 0 ? "<-HIT" : "")}");
        }
    }

    static void Main()
    {
        var common = new AttackData { attack = "10", canHitSameTarget = 1,
            targetActionChange = new ActionChangeData { changeType = 1, param = "hurt_1", priority = 100 } };

        var stun = common; stun.hitStun = "0.4";
        Run("== 场景A 僵直：hitStun 0.4s = 6 帧，hurt_1 只有 3 帧需循环；受击动作开放 attack 取消窗口 ==", stun, 14);

        OpenWindow = false;
        Run("== 场景C 僵直：受击动作无取消窗口，僵直结束应直接回到 idle ==", stun, 9);

        var launch = common; launch.launchVelocity = "8";
        launch.launchActionChange = new ActionChangeData { changeType = 1, param = "hurt_air", priority = 100 };
        Run("== 场景B 击飞：launchVelocity 8，hurt_air 循环直到落地 -> onLand hurt_land -> idle ==", launch, 14);
    }
}
