using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 命中结算 (纯确定性)
    ///
    /// 流程：
    /// 1. 命中记录、扣血 (霸体抵挡、被抓中同样扣血)
    /// 2. 被抓中的目标：只结算伤害，保持原抓取状态 (不受击退/击飞/外力影响，也不会被其他抓取覆盖)
    ///    投掷做法：抓取锚点在第 N 帧消失 (先于命中检测释放)，同一帧开启的攻击即按正常受击结算
    /// 3. 霸体裁决：攻击破霸等级 &lt; 受击者霸体等级 -> 只结算伤害，不产生任何受击反应
    /// 4. 受击反应 (按优先级)，未配置 hurtAction 时使用 HurtActionIds 中的固定动作：
    ///    - 抓取：目标锁定到攻击者锚点，按 grab.pose 进入被抓姿势 (未配置用 hurtAction，再没有则 grabbed)
    ///    - 空中受击：目标已浮空时所有攻击都按 air (未配置用 GameConfig.DefaultAirLaunch) 重设速度，播 float (从 airHitStep 切片步起)
    ///    - 地面击飞：配置了 launch (默认 float 从头播放)；倒地目标被 canHitLying 的击飞攻击挑起
    ///    - 倒地中被扫地且未挑起：保持倒地，只结算伤害
    ///    - 僵直：地面受击 (默认在 hurt / hurt_1 / hurt_2 / hurt_3 中确定性随机)，附加平推 push
    ///    吸附 (suction) 在僵直 / 击飞 / 空中受击时生效
    /// 5. 双方动作变化与临时取消窗口 (动作切换在 BattleWorld 第 6 步统一裁决)
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

            // 技能 (含技能生成物) 命中为主人获得查克拉 (每次技能刷新只获得一次)
            attacker.OnHitDealt();

            HitReaction reaction;
            if (target.Hurt == HurtState.Grabbed)
            {
                reaction = HitReaction.GrabbedHit;
                // 抓取者的后续抓取段带姿势：按新姿势切换 (只换动作，不改变抓取状态)
                if (target.GrabbedBy == attacker && atk.grab.enabled && atk.grab.pose.Enabled)
                {
                    target.action.PreorderActionByActionChangeInfo(PoseChange(target, atk.grab.pose));
                }
            }
            else
            {
                reaction = atk.armorBreak >= target.action.CurrentArmor
                    ? ApplyReaction(hit, attacker, target, atk, frameId)
                    : HitReaction.ArmorBlocked;
            }

            // 攻击方：命中即生效 (无论是否被霸体抵挡或目标处于被抓中)
            attacker.action.RecordHit(hit.Frame);
            attacker.action.PreorderActionByActionChangeInfo(atk.selfActionChange);
            if (atk.tempBeCancelledTagTurnOn != null)
            {
                foreach (string id in atk.tempBeCancelledTagTurnOn) attacker.action.AddTempBeCancelledTag(id);
            }

            return new HitEvent
            {
                FrameId = frameId,
                AttackerId = attacker.EntityId,
                OwnerId = attacker.RootOwner.EntityId,
                TargetId = target.EntityId,
                AttackPhase = hit.AttackPhase,
                Damage = atk.attack,
                HitPoint = hit.HitPoint,
                Reaction = reaction
            };
        }

        private static HitReaction ApplyReaction(HitResult hit, CharacterObj attacker, CharacterObj target, AttackInfo atk, uint frameId)
        {
            HitReaction reaction;
            bool hasHurtAction = !string.IsNullOrEmpty(atk.hurtAction);
            ActionChangeInfo targetChange = default;

            if (atk.grab.enabled)
            {
                attacker.StartGrab(target, atk.grab);
                targetChange = atk.grab.pose.Enabled
                    ? PoseChange(target, atk.grab.pose)
                    : HurtActionIds.ChangeTo(hasHurtAction ? atk.hurtAction : HurtActionIds.Grabbed);
                reaction = HitReaction.Grab;
            }
            else
            {
                if (target.IsAirborne)
                {
                    Launch(attacker, target, atk.air.enabled ? atk.air : GameConfig.DefaultAirLaunch);
                    targetChange = HurtActionIds.ChangeTo(HurtActionIds.Float, fromFrame: target.action.FloatAirHitFrame);
                    reaction = HitReaction.Launch;
                }
                else if (atk.launch.enabled)
                {
                    Launch(attacker, target, atk.launch);
                    targetChange = HurtActionIds.ChangeTo(hasHurtAction ? atk.hurtAction : HurtActionIds.Float);
                    reaction = HitReaction.Launch;
                }
                else if (target.IsLying)
                {
                    reaction = HitReaction.Lying;
                }
                else
                {
                    // 未配置僵直时长与受击动作时不进入僵直，也不切换受击动作
                    int stunTicks = GameConfig.SecondsToTicks(atk.hitStun);
                    target.EnterStun(stunTicks);
                    if (hasHurtAction || stunTicks > 0)
                    {
                        string stand = hasHurtAction
                            ? atk.hurtAction
                            : HurtActionIds.PickStand(target.action.StandHurtVariants, frameId, target.EntityId, attacker.EntityId);
                        targetChange = HurtActionIds.ChangeTo(stand);
                    }
                    if (atk.push.inSec > FP.Zero) target.SetForceMove(atk.push.ToWorld(attacker.FacingLeft));
                    reaction = HitReaction.Stun;
                }

                if (reaction != HitReaction.Lying && atk.suction.Enabled) target.StartSuction(attacker, atk.suction);
            }

            target.action.PreorderActionByActionChangeInfo(targetChange);
            attacker.action.PreorderActionByActionChangeInfo(hit.Defense.attackerActionChange);
            target.action.PreorderActionByActionChangeInfo(hit.Defense.selfActionChange);
            if (hit.Defense.tempBeCancelledTagTurnOn != null)
            {
                foreach (string id in hit.Defense.tempBeCancelledTagTurnOn) target.action.AddTempBeCancelledTag(id);
            }

            return reaction;
        }

        /// <summary>
        /// 被抓姿势：切换到 pose.action，从其第 pose.step 个切片步开始，按 pose.hold 定格
        /// </summary>
        private static ActionChangeInfo PoseChange(CharacterObj target, GrabPoseInfo pose)
        {
            return HurtActionIds.ChangeTo(pose.action, fromFrame: target.action.FirstFrameOfStep(pose.action, pose.step), hold: pose.hold);
        }

        /// <summary>
        /// 击飞：设置竖直速度并覆盖水平惯性 (攻击者本地方向 -> 世界)，进入击飞状态
        /// </summary>
        private static void Launch(CharacterObj attacker, CharacterObj target, LaunchInfo launch)
        {
            target.motion.Launch(launch.z);
            target.EnterLaunch();
            target.motion.SetPlanarVelocity(attacker.FacingLeft ? -launch.x : launch.x, launch.y);
        }
    }
}
