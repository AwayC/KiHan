using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;
using KiHan.Logic;

static class Program
{
    static AnimationFrameData Anim(int frames)
    {
        var a = new AnimationFrameData();
        for (int i = 0; i < frames; i++) a.Steps.Add(new AnimationFrameData.AnimStep { FrameIndex = 0, Duration = 1 });
        return a;
    }

    static ActionInfo Act(string id, string next) => new ActionInfo { id = id, animKey = id, autoNextActionId = next };

    static int Scenario;
    static long Run(bool print)
    {
        var anim = new FrameAnimator();
        foreach (var k in new[] { "jump", "fall", "land", "idle" }) anim.RegisterAnim(k, Anim(k == "fall" ? 4 : k == "jump" ? 12 : 6));

        var jump = Act("jump", "fall");
        jump.rootMotion = Scenario == 0
            ? new[]
            {
                new RootMotionSegmentData { frameRange = new FrameRange(3, 6), moveX = "1.2", moveY = "0", tweenMethod = "Linear" }.ToRuntime(),
                new RootMotionSegmentData { frameRange = new FrameRange(1, 1), velocityZ = "8" }.ToRuntime(),
                new RootMotionSegmentData { frameRange = new FrameRange(4, 7), accelerationZ = "40" }.ToRuntime()
            }
            : new[]
            {
                new RootMotionSegmentData { frameRange = new FrameRange(0, 2), accelerationZ = "100" }.ToRuntime()
            };
        var fall = Act("fall", "fall");
        fall.keepPlayingAnim = true;
        fall.onLand = new ActionChangeInfo { changeType = ActionChangeType.ChangeToActionId, param = "land", priority = 100 };
        var land = Act("land", "idle");
        var idle = Act("idle", "idle");

        var ac = new ActionController { anim = anim };
        var cha = new CharacterObj { action = ac, EntityId = 1 };
        ac.SetAllActions(new List<ActionInfo> { jump, fall, land, idle }, "jump");

        var world = new BattleWorld(new MapLogic());
        world.AddCharacter(cha);
        long hash = 17;
        for (uint f = 1; f <= 18; f++)
        {
            world.Tick(new RoomFrame { FrameId = f });
            hash = hash * 31 + cha.motion.Position.z.RawValue;
            if (print)
                Console.WriteLine($"f{f,2} x={cha.motion.Position.x.AsFloat(),5:F2} action={ac.CurrentAction.id,-5} frame={anim.ElapsedFrames,2} z={cha.motion.Position.z.AsFloat(),6:F3} vz={cha.motion.VelocityZ.AsFloat(),7:F3} {cha.LastPhysicsEvent}");
        }
        return hash;
    }

    static int Main()
    {
        Console.WriteLine("== 场景A: 第1帧起跳 v=8，帧4~7 附加加速度 40 (=重力) 悬停，Linear 第3~6帧前进 1.2 ==");
        Scenario = 0; long h1 = Run(true), h2 = Run(false);
        Console.WriteLine($"hash1={h1} hash2={h2} same={h1 == h2}");
        Console.WriteLine("== 场景B: 地面帧0~2 附加加速度 100 (>重力) 推升离地 ==");
        Scenario = 1; Run(true);
        return h1 == h2 ? 0 : 1;
    }
}
