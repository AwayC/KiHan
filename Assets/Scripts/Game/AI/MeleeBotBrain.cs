using KiHan.Math;
using TrueSync;

namespace KiHan.Logic.AI
{
    /// <summary>
    /// 简单近战人机 (训练场"敌人攻击")：
    /// <code>
    /// 选择
    /// ├─ 顺序：无法行动 (受击 / 被控 / 死亡 / 没有目标) → 不输入
    /// ├─ 顺序：目标不在攻击距离 → 走向目标 (横向 + 纵深对齐)
    /// ├─ 顺序：到了出招节奏且有可用技能 → 按技能键
    /// └─ 普攻：背对目标先转身，否则隔帧点按普攻 (每次按下推进一段连招)
    /// </code>
    /// 节奏只取决于逻辑帧号，相同局面产生相同输入
    /// </summary>
    public static class MeleeBotBrain
    {
        /// <summary>
        /// 进入普攻的横向距离与纵深对齐容差 (逻辑单位)
        /// </summary>
        private static readonly FP ReachX = FP.Parse("1.1");
        private static readonly FP ReachY = FP.Parse("0.25");

        /// <summary>
        /// 尝试放技能的间隔 (逻辑帧)
        /// </summary>
        private const int SkillInterval = 45;

        private static readonly CommandKey[] SkillKeys = { CommandKey.Skill1, CommandKey.Skill2, CommandKey.Ultimate };
        private static readonly ButtonMask[] SkillButtons = { ButtonMask.Skill1, ButtonMask.Skill2, ButtonMask.Ultimate };

        public static IBotBrain Create()
        {
            return new BehaviorTreeBrain(new BTSelector(
                new BTSequence(new BTCondition(CannotAct), new BTAction(Idle)),
                new BTSequence(new BTCondition(OutOfReach), new BTAction(Approach)),
                new BTSequence(new BTCondition(IsSkillBeat), new BTAction(CastSkill)),
                new BTAction(Attack)));
        }

        private static bool CannotAct(BotContext ctx)
        {
            CharacterObj self = ctx.Self;
            return ctx.Target == null || self.IsDead || self.Hurt != HurtState.None || self.action == null || self.action.ControlLocked;
        }

        private static BTStatus Idle(BotContext ctx) => BTStatus.Success;

        private static bool OutOfReach(BotContext ctx)
        {
            FPVector3 d = ctx.Target.motion.Position - ctx.Self.motion.Position;
            return FPMath.Abs(d.x) > ReachX || FPMath.Abs(d.y) > ReachY;
        }

        private static BTStatus Approach(BotContext ctx)
        {
            FPVector3 d = ctx.Target.motion.Position - ctx.Self.motion.Position;
            if (FPMath.Abs(d.x) > ReachX) ctx.MoveX = d.x > FP.Zero ? 1 : -1;
            if (FPMath.Abs(d.y) > ReachY) ctx.MoveY = d.y > FP.Zero ? 1 : -1;
            return BTStatus.Running;
        }

        private static bool IsSkillBeat(BotContext ctx) => ctx.FrameId % SkillInterval == 0;

        private static BTStatus CastSkill(BotContext ctx)
        {
            SkillController skills = ctx.Self.Skills;
            if (skills == null) return BTStatus.Failure;

            // 从帧号决定的起点轮流尝试，避免总是同一个技能
            int start = (int)(ctx.FrameId / SkillInterval % (uint)SkillKeys.Length);
            for (int k = 0; k < SkillKeys.Length; k++)
            {
                int slot = (start + k) % SkillKeys.Length;
                int index = skills.IndexOfButton(SkillKeys[slot]);
                if (index < 0 || !IsReady(skills, index)) continue;
                ctx.Buttons |= SkillButtons[slot];
                return BTStatus.Success;
            }
            return BTStatus.Failure;
        }

        private static bool IsReady(SkillController skills, int index)
        {
            SkillInfo skill = skills.GetSkill(index);
            return !skill.requireHurt && skills.GetCooldown(index) == 0 && skills.GetNextStage(index) == 0 && skills.Chakra >= skill.chakraCost;
        }

        private static BTStatus Attack(BotContext ctx)
        {
            FP dx = ctx.Target.motion.Position.x - ctx.Self.motion.Position.x;
            bool targetOnLeft = dx < FP.Zero;
            if (dx != FP.Zero && targetOnLeft != ctx.Self.FacingLeft)
            {
                ctx.MoveX = targetOnLeft ? -1 : 1;
                return BTStatus.Running;
            }

            // 隔帧松开：每次按下都是新的按键事件，连招逐段推进
            if ((ctx.FrameId & 1) == 0) ctx.Buttons |= ButtonMask.Attack;
            return BTStatus.Running;
        }
    }
}
