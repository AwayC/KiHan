using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 命中结算 (纯确定性)
    ///
    /// 流程：
    /// 1. 命中记录、扣血 (霸体抵挡同样扣血)
    /// 2. 霸体裁决：攻击破霸等级 &lt; 受击者霸体等级 -> 只结算伤害，不产生任何受击反应
    /// 3. 受击反应 (按优先级)，受击动作未配置时使用 HurtActionIds 中的固定动作：
    ///    - 抓取：目标锁定到攻击者锚点 (默认 hurt_2)
    ///    - 击飞：配置了击飞速度，或目标已在空中 (空中追击优先使用 airLaunchVelocity，默认 hurt_toair)
    ///    - 倒地中被扫地且未挑起：保持倒地，只结算伤害
    ///    - 僵直：地面受击 (默认 hurt_1)
    ///    以及平推 (pushPower) 与吸附 (suction) 两种水平外力
    /// 4. 双方动作变化与临时取消窗口 (动作切换在 BattleWorld 第 6 步统一裁决)
    /// </summary>
    public static class HitResolver
    {
        public static HitEvent Resolve(HitResult hit, uint frameId)
        {
            CharacterObj attacker = hit.Attacker;
            CharacterObj target = hit.Target;
            AttackInfo atk = hit.Attack;

            attacker.AddHitRecord(target, hit.AttackPhase);
            target.TakeDamage(atk.attack);

            HitReaction reaction = atk.armorBreak >= target.action.CurrentArmor
                ? ApplyReaction(hit, attacker, target, atk)
                : HitReaction.ArmorBlocked;

            // 攻击方：命中即生效 (无论是否被霸体抵挡)
            attacker.action.PreorderActionByActionChangeInfo(atk.selfActionChange, atk.forceDir);
            if (atk.tempBeCancelledTagTurnOn != null)
            {
                foreach (string id in atk.tempBeCancelledTagTurnOn) attacker.action.AddTempBeCancelledTag(id);
            }

            return new HitEvent
            {
                FrameId = frameId,
                AttackerId = attacker.EntityId,
                TargetId = target.EntityId,
                AttackPhase = hit.AttackPhase,
                Damage = atk.attack,
                HitPoint = hit.HitPoint,
                Reaction = reaction
            };
        }

        private static HitReaction ApplyReaction(HitResult hit, CharacterObj attacker, CharacterObj target, AttackInfo atk)
        {
            HitReaction reaction;
            bool hasTargetChange = atk.targetActionChange.changeType != ActionChangeType.Keep;
            ActionChangeInfo targetChange;

            if (atk.grab.enabled)
            {
                attacker.StartGrab(target, atk.grab);
                targetChange = hasTargetChange ? atk.targetActionChange : HurtActionIds.ChangeTo(HurtActionIds.Heavy);
                reaction = HitReaction.Grab;
            }
            else
            {
                bool airborne = target.IsAirborne;
                FP velocity = airborne && atk.airLaunchVelocity > FP.Zero ? atk.airLaunchVelocity : atk.launchVelocity;

                if (velocity > FP.Zero || airborne)
                {
                    if (velocity > FP.Zero) target.motion.Launch(velocity);
                    target.EnterLaunch();
                    targetChange = atk.launchActionChange.changeType != ActionChangeType.Keep
                        ? atk.launchActionChange
                        : HurtActionIds.ChangeTo(HurtActionIds.ToAir);
                    reaction = HitReaction.Launch;
                }
                else if (target.IsLying)
                {
                    targetChange = default;
                    reaction = HitReaction.Lying;
                }
                else
                {
                    // 未配置僵直时长时不进入僵直，也不套用默认受击动作
                    int stunTicks = GameConfig.SecondsToTicks(atk.hitStun);
                    target.EnterStun(stunTicks);
                    targetChange = hasTargetChange || stunTicks <= 0
                        ? atk.targetActionChange
                        : HurtActionIds.ChangeTo(HurtActionIds.Light);
                    reaction = HitReaction.Stun;
                }

                // 水平外力：平推 (配置为攻击者本地方向) 与吸附 (向攻击者核心点拉扯)
                if (atk.pushPower.inSec > FP.Zero) target.SetForceMove(atk.pushPower.ToWorld(attacker.FacingLeft));
                if (atk.suction.Enabled) target.StartSuction(attacker, atk.suction);
            }

            target.action.PreorderActionByActionChangeInfo(targetChange, atk.forceDir);
            attacker.action.PreorderActionByActionChangeInfo(hit.Defense.attackerActionChange, atk.forceDir);
            target.action.PreorderActionByActionChangeInfo(hit.Defense.selfActionChange, atk.forceDir);
            if (hit.Defense.tempBeCancelledTagTurnOn != null)
            {
                foreach (string id in hit.Defense.tempBeCancelledTagTurnOn) target.action.AddTempBeCancelledTag(id);
            }

            return reaction;
        }
    }
}
