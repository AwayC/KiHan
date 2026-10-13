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

    static CharacterObj Make(int id, int team, FPVector3 pos, bool facingLeft, string tween)
    {
        var hurtBox = new LogicBox(new Vector2Int(0, 80), new Vector2Int(60, 160), 40);
        var anim = new FrameAnimator();
        anim.RegisterAnim("attack", Anim(30, new LogicBox(new Vector2Int(60, 80), new Vector2Int(80, 60), 40), hurtBox));
        anim.RegisterAnim("hurt", Anim(4, null, hurtBox));
        anim.RegisterAnim("idle", Anim(6, null, hurtBox));

        var attack = new ActionInfo { id = "attack", animKey = "attack", autoNextActionId = "attack",
            attackPhase = new[] { new AttackBoxTurnOnInfo { frameRanges = new[] { new FrameRange(0, 999) }, attackPhase = 0 } },
            attacks = new[] { new AttackData {
                attack = "10", freeze = "0.07", canHitSameTarget = 99, hitSameTargetDelay = "0.2",
                pushPower = new MoveInfoData { moveX = "0.6", inSec = "0.2", tweenMethod = tween },
                targetActionChange = new ActionChangeData { changeType = 2, param = "Hurt", priority = 100 } }.ToRuntime() } };
        var hurt = new ActionInfo { id = "hurt", animKey = "hurt", catalog = "Hurt", autoNextActionId = "idle" };
        var idle = new ActionInfo { id = "idle", animKey = "idle", autoNextActionId = "idle", keepPlayingAnim = true };

        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = id, TeamId = team };
        ac.Set((a, b) => cha.HitRecords.Clear());
        ac.FacingLeft = facingLeft;
        ac.SetAllActions(new List<ActionInfo> { attack, hurt, idle }, id == 1 ? "attack" : "idle");
        cha.motion.Reset(pos);
        return cha;
    }

    static void Run(string tween, int frames, bool print, out long alloc, out int hits)
    {
        var world = new BattleWorld(new MapLogic());
        var a = Make(1, 1, new FPVector3(FP.Parse("6.1"), FP.Zero, FP.Zero), false, tween);
        var t = Make(2, 2, new FPVector3(FP.Parse("6.9"), FP.Zero, FP.Zero), false, tween);
        world.AddCharacter(a); world.AddCharacter(t);
        alloc = 0; hits = 0;
        var frame = new RoomFrame();
        for (uint f = 1; f <= frames; f++)
        {
            frame.FrameId = f;
            long before = GC.GetAllocatedBytesForCurrentThread();
            world.Tick(frame);
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            if (f > 20) { alloc += used; hits += world.HitEvents.Count; if (used > 0 && !print) Console.WriteLine($"  f{f}: +{used}B hit={world.HitEvents.Count}"); }
            if (print)
                Console.WriteLine($"f{f,2} 目标x={t.motion.Position.x.AsFloat(),6:F3} 动作={t.action.CurrentAction.id,-6} 命中={(world.HitEvents.Count > 0 ? "HIT" : "")}");
        }
    }

    static int Main()
    {
        Console.WriteLine("== 目标贴右墙(x=7)：Slowly 0.6 / 0.2s，每 0.2s 可再次命中 ==");
        Run("Slowly", 12, true, out _, out _);
        Run("Slowly", 60, false, out _, out _); // 预热 JIT
        Run("Slowly", 1020, false, out long alloc, out int hits);
        Console.WriteLine($"热身后 1000 帧：命中 {hits} 次，托管分配 {alloc} bytes");
        return alloc == 0 && hits > 0 ? 0 : 1;
    }
}
