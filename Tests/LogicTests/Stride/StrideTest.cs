using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using KiHan.Logic;
using UnityEngine;

static class Program
{
    // 每个动画帧一个切片步；hitFrames 中的帧带攻击盒
    static AnimationFrameData Anim(int frames, int[] hitFrames)
    {
        var lib = new CharacterSpriteLibrary();
        var hurt = new LogicBox(new Vector2Int(0, 80), new Vector2Int(60, 160), 40);
        var a = new AnimationFrameData { Library = lib };
        for (int i = 0; i < frames; i++)
        {
            var fd = new SpriteFrameData();
            fd.HurtBoxes.Add(hurt);
            if (Array.IndexOf(hitFrames, i) >= 0) fd.HitBoxes.Add(new LogicBox(new Vector2Int(60, 80), new Vector2Int(80, 60), 40));
            lib.AllFrames.Add(fd);
            a.Steps.Add(new AnimationFrameData.AnimStep { FrameIndex = i, Duration = 1 });
        }
        return a;
    }

    static int[] HitFrames = { 3 };
    static CharacterObj Make(int id, string start)
    {
        var anim = new FrameAnimator();
        anim.RegisterAnim("attack", Anim(12, HitFrames));
        anim.RegisterAnim("hurt_1", Anim(3, new int[0]));
        anim.RegisterAnim("idle", Anim(4, new int[0]));
        var attack = new ActionInfo { id = "attack", animKey = "attack", autoNextActionId = "idle",
            attackPhase = new[] { new AttackBoxTurnOnInfo { frameRanges = new[] { new FrameRange(0, 11) }, attackPhase = 0 } },
            attacks = new[] { new AttackData { attack = "10", canHitSameTarget = 1, hitStun = "0.4",
                targetActionChange = new ActionChangeData { changeType = 1, param = "hurt_1", priority = 100 } }.ToRuntime() } };
        var hurt1 = new ActionInfo { id = "hurt_1", animKey = "hurt_1", autoNextActionId = "idle" };
        var idle = new ActionInfo { id = "idle", animKey = "idle", autoNextActionId = "idle", keepPlayingAnim = true };
        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = id, TeamId = id };
        ac.SetAllActions(new List<ActionInfo> { attack, hurt1, idle }, start);
        cha.motion.Reset(new FPVector3(id == 1 ? FP.Zero : FP.Parse("0.9"), FP.Zero, FP.Zero));
        return cha;
    }

    static void Main()
    {
        Run("== A: 12 帧攻击动作，攻击盒只画在第 3 帧 (显示时被跳过的奇数帧)；僵直 0.4s，hurt_1 3 帧 ==", new[] { 3 }, 9);
        Run("== B: 攻击盒只画在最后一帧 (第 11 帧)，该逻辑帧播完即 autoNext ==", new[] { 11 }, 13);
    }

    static void Run(string title, int[] hitFrames, int frames)
    {
        HitFrames = hitFrames;
        Console.WriteLine(title);
        var world = new BattleWorld(new MapLogic());
        var a = Make(1, "attack"); var t = Make(2, "idle");
        world.AddCharacter(a); world.AddCharacter(t);
        var frame = new RoomFrame();
        for (uint f = 1; f <= frames; f++)
        {
            frame.FrameId = f;
            world.Tick(frame);
            Console.WriteLine($"  逻辑帧{f} 攻击者={a.action.CurrentAction.id,-6} 处理动画帧[{a.action.anim.TickFromFrame},{a.action.anim.TickToFrame}) | 受击者={t.action.CurrentAction.id,-6} 动画帧={t.action.anim.ElapsedFrames} 状态={t.Hurt,-5} 僵直剩余={t.StunTicks} {(world.HitEvents.Count > 0 ? "<-HIT" : "")}");
        }
    }
}
