using System;
using System.Collections.Generic;
using UnityEngine;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 角色动作与战斗实体逻辑主体 (纯定点数确定性运算)
    /// 由 BattleWorld 按 EntityId 顺序显式驱动，不依赖 Unity Update 与物理系统
    /// </summary>
    public class CharacterObj : MonoBehaviour
    {
        /// <summary>
        /// 逻辑实体唯一 ID (决定遍历与结算顺序，必须在双端一致)
        /// </summary>
        public int EntityId;

        /// <summary>
        /// 阵营 ID，同阵营之间不产生命中
        /// </summary>
        public int TeamId;

        /// <summary>
        /// 对应 RoomFrame.InputFrames 的玩家槽位，-1 表示不接收网络输入 (如分身/AI)
        /// </summary>
        public int InputSlot = -1;

        /// <summary>
        /// 角色配置 ID (如 90001 鸣人)：查找生成物配置与表现资源
        /// </summary>
        public int CharacterId;

        /// <summary>
        /// 所在的逻辑世界 (加入 BattleWorld 时设置)：动作事件与生成请求的接收者
        /// </summary>
        public BattleWorld World { get; internal set; }

        /// <summary>
        /// 技能资源控制器 (玩家角色持有；生成物为 null，命中的查克拉归属主人)
        /// </summary>
        public SkillController Skills { get; set; }

        /// <summary>
        /// 生成物配置 (玩家角色为 null)
        /// </summary>
        public SpawnInfo Spawn { get; set; }

        /// <summary>
        /// 生成物的主人 (最初的玩家角色，玩家角色为 null)
        /// </summary>
        public CharacterObj Owner { get; internal set; }

        /// <summary>
        /// 生成物来源技能 (主人 SkillController 中的下标，-1 表示不是技能生成的)
        /// </summary>
        public int SourceSkill { get; internal set; } = -1;

        /// <summary>
        /// 生成物剩余寿命 (逻辑帧)，-1 表示不限
        /// </summary>
        public int LifeTicks { get; internal set; } = -1;

        /// <summary>
        /// 生成物正在播放消失动作 (期间无敌，播完回收)
        /// </summary>
        public bool Despawning { get; private set; }

        /// <summary>
        /// 分身大脑 (Actor 生成物持有，在输入阶段生成本帧输入)
        /// </summary>
        public CloneBrain Brain { get; set; }

        /// <summary>
        /// 身上的 Buff (只计时与通知表现，不修改属性)
        /// </summary>
        public readonly BuffController Buffs = new BuffController();

        /// <summary>
        /// 战绩、查克拉归属的根角色
        /// </summary>
        public CharacterObj RootOwner => Owner != null ? Owner : this;

        /// <summary>
        /// 能否被命中 (简单生成物没有受击)
        /// </summary>
        public bool Hittable => Spawn == null || Spawn.Hittable;

        [SerializeField] private ActionController _action;
        [SerializeField] private InputToCommand _input;

        /// <summary>
        /// 动作控制器 (设置时自动把当前输入组件同步给它)
        /// </summary>
        public ActionController action
        {
            get => _action;
            set
            {
                _action = value;
                SyncInputToAction();
            }
        }

        /// <summary>
        /// 指令与输入系统：输入组件只在这里设置，动作控制器的指令判定引用由此统一同步
        /// </summary>
        public InputToCommand input
        {
            get => _input;
            set
            {
                _input = value;
                SyncInputToAction();
            }
        }

        private void SyncInputToAction()
        {
            if (_action != null)
            {
                _action.command = _input;
                _action.Owner = this;
            }
        }

        /// <summary>
        /// 确定性运动体 (位置、竖直速度、滞空状态)
        /// </summary>
        public readonly MotionBody motion = new MotionBody();

        /// <summary>
        /// 横向移动速度 (定点数，单位/秒)
        /// </summary>
        public FP moveSpeedX = FP.Parse("5.0");

        /// <summary>
        /// 纵深移动速度 (定点数，单位/秒)：与横向各自独立，斜向两轴同时全速
        /// </summary>
        public FP moveSpeedY = FP.Parse("5.0");

        /// <summary>
        /// 重力加速度 (定点数，单位/秒²)
        /// </summary>
        public FP gravity = FP.Parse("40");

        /// <summary>
        /// 最大生命值 (定点数)
        /// </summary>
        public FP maxHp = FP.Parse("1000");

        /// <summary>
        /// 调试无限血：受击照常扣血与结算，但生命值不低于 1 (不会因死亡而无法再被命中)
        /// </summary>
        public bool infiniteHp = false;

        /// <summary>
        /// 击飞触地弹起的速度衰减系数：弹起速度 = 落地瞬间下落速度 × 该系数
        /// </summary>
        public FP bounceRestitution = FP.Parse("0.5");

        /// <summary>
        /// 最小弹起速度 (单位/秒)：衰减后低于该值不再弹起，转为倒地
        /// </summary>
        public FP minBounceVelocity = FP.Parse("3");

        /// <summary>
        /// 起身动作结束后追加的无敌时长 (定点数秒，0 表示只有起身期间无敌)
        /// </summary>
        public FP standupInvincibleSec = FP.Parse("0.5");

        /// <summary>
        /// 剩余的起身后无敌逻辑帧数
        /// </summary>
        public int InvincibleTicks { get; private set; }

        /// <summary>
        /// 当前生命值 (由 ResetCombatState 初始化为 maxHp)
        /// </summary>
        public FP Hp { get; private set; }

        public bool IsDead => Hp <= FP.Zero;

        /// <summary>
        /// 当前动作对各目标的命中冷却记录 (换动作时重置；值类型按下标原地更新，预留容量避免扩容)
        /// </summary>
        public List<HitRecord> HitRecords { get; private set; } = new List<HitRecord>(8);

        /// <summary>
        /// 本帧产生的物理事件
        /// </summary>
        public PhysicsEvent LastPhysicsEvent { get; private set; }

        /// <summary>
        /// 当前受击状态 (僵直/击飞期间不能主动行动)
        /// </summary>
        public HurtState Hurt { get; private set; } = HurtState.None;

        /// <summary>
        /// 剩余僵直逻辑帧数 (仅 Hurt == Stun 时有效)
        /// </summary>
        public int StunTicks { get; private set; }

        /// <summary>
        /// 抓住自己的角色 (仅 Hurt == Grabbed 时有效)
        /// </summary>
        public CharacterObj GrabbedBy { get; private set; }

        /// <summary>
        /// 自己正在抓取的目标
        /// </summary>
        public CharacterObj GrabTarget { get; private set; }

        private string _grabAnchorName;

        /// <summary>
        /// 最近一次读取到的抓取锚点 (本地逻辑坐标，未按朝向镜像)；本帧未推进动画帧时沿用
        /// </summary>
        private FPVector3 _grabAnchorLocal;
        private ActionChangeInfo _grabRelease;

        /// <summary>
        /// 吸附来源与配置 (值类型/引用原地复用，0 GC)
        /// </summary>
        private CharacterObj _suctionSource;

        /// <summary>
        /// 当前吸附来源 (来源移出战场时由 BattleWorld 解除)
        /// </summary>
        public CharacterObj SuctionSource => _suctionSource;

        private SuctionInfo _suction;
        private FP _suctionElapsed;

        /// <summary>
        /// 当前施加的强制外力位移 (世界空间，值类型原地复用)
        /// </summary>
        private ForceMove _forceMove;

        private bool UnderForceMove => _forceMove.Active;

        /// <summary>
        /// 是否面朝左 (权威状态在 ActionController，判定盒、RootMotion、外力与锚点水平镜像)
        /// 不依赖输入组件，AI 分身、召唤物等无输入的逻辑实体同样可以转向
        /// </summary>
        public bool FacingLeft
        {
            get => action != null && action.FacingLeft;
            set
            {
                if (action != null) action.FacingLeft = value;
            }
        }

        public bool IsAirborne => motion.IsAirborne;

        /// <summary>
        /// 是否倒地，倒地时只有 canHitLying 的攻击能命中
        /// </summary>
        public bool IsLying => Hurt == HurtState.Lying;

        /// <summary>
        /// 是否无敌 (起身期间与起身后的追加时间)：不产生任何命中，不受伤害与任何效果
        /// </summary>
        public bool IsInvincible => Hurt == HurtState.Standup || InvincibleTicks > 0 || Despawning;

        private void Awake()
        {
            SyncInputToAction();
        }

        #region 动作回调 (ActionController 调用)

        /// <summary>
        /// 动作切换：清除命中记录，技能段流转；进入受击强制动作 (替身) 时解除受击；
        /// 切换到其他动画时清除水平惯性 (击飞流程中的受击动作切换、同一段动画的 keepFrame 变体除外)
        /// </summary>
        public void OnActionChanged(ActionInfo previous, ActionInfo current, bool keepFrame)
        {
            HitRecords.Clear();
            Skills?.OnActionChanged(current.id);
            if (current.breakHurt) BreakHurt();
            if (!keepFrame && Hurt != HurtState.Launch) motion.ClearPlanarVelocity();
        }

        /// <summary>
        /// 技能准入 (主动打断与受击强制进入时调用)
        /// </summary>
        public bool CanEnterAction(in ActionInfo info)
        {
            return Skills == null || Skills.CanEnter(info.id, Hurt);
        }

        /// <summary>
        /// 动作事件：交给所在世界 (生成请求 / 表现事件)
        /// </summary>
        public void OnActionEvent(in ActionEventInfo evt)
        {
            World?.OnActionEvent(this, in evt);
        }

        /// <summary>
        /// 当前行为归属的技能 (查克拉与生成物来源)：生成物取来源技能，玩家角色取当前动作所属技能
        /// </summary>
        public int AttributedSkill()
        {
            if (Owner != null) return SourceSkill;
            return Skills != null && action != null ? Skills.SkillIndexOfAction(action.CurrentAction.id) : -1;
        }

        /// <summary>
        /// 命中结算后调用：技能 (或技能生成物) 命中为主人获得查克拉
        /// </summary>
        public void OnHitDealt()
        {
            SkillController skills = RootOwner.Skills;
            skills?.TryGainChakra(AttributedSkill());
        }

        /// <summary>
        /// 事件点：本逻辑帧最后处理的动画帧上的锚点 (缺省或不存在时为脚底) 加像素偏移，转换为逻辑坐标 (x 随朝向镜像)
        /// </summary>
        public FPVector3 EventPoint(string anchor, Vector2Int offset)
        {
            Vector2Int px = offset;
            FrameAnimator anim = action != null ? action.anim : null;
            if (anim != null && !string.IsNullOrEmpty(anchor))
            {
                int frame = anim.TickToFrame > anim.TickFromFrame ? anim.TickToFrame - 1 : anim.TickToFrame;
                if (anim.TryGetAnchorAtFrame(frame, anchor, out Vector2Int anchorPx)) px += anchorPx;
            }

            FP x = GameConfig.PixelToUnit(FacingLeft ? -px.x : px.x);
            FPVector3 pos = motion.Position + new FPVector3(x, FP.Zero, GameConfig.PixelToUnit(px.y));
            if (pos.z < FP.Zero) pos.z = FP.Zero;
            return pos;
        }

        #endregion

        #region 逻辑帧驱动

        /// <summary>
        /// 动作推进：默认以 GameConfig.LOGIC_TICK_TIME_FP 推进
        /// </summary>
        public void Tick() => Tick(GameConfig.LOGIC_TICK_TIME_FP);

        public void Tick(FP deltaFP)
        {
            if (InvincibleTicks > 0) InvincibleTicks--;

            // 技能 CD 与接段时限
            Skills?.Tick();

            // 生成物寿命：到期播放消失动作 (没有消失动作时由 BattleWorld 直接回收)
            TickLifetime();

            // Buff 计时，到期移除
            Buffs.Tick(this);

            // 僵直倒计时：命中帧之后再经过 StunTicks 帧解除，解除当帧回到默认动作
            if (Hurt == HurtState.Stun)
            {
                StunTicks--;
                if (StunTicks <= 0) ExitHurt();
            }

            // 推进动作状态机
            action?.Tick();

            // 受击流程：受击动作播完时由框架决定下一个动作
            UpdateHurtFlow();

            // 推进命中冷却倒计时
            {
                for (int i = 0; i < HitRecords.Count; i++)
                {
                    HitRecord rec = HitRecords[i];
                    rec.Tick(deltaFP);
                    HitRecords[i] = rec;
                }
            }
        }

        /// <summary>
        /// 物理推进：外力/自然位移 + 重力 + 地面 + 边界墙，并把落地等事件反馈给动作控制器
        /// </summary>
        public PhysicsEvent PhysicsTick(MapLogic map) => PhysicsTick(map, GameConfig.LOGIC_TICK_TIME_FP);

        public PhysicsEvent PhysicsTick(MapLogic map, FP deltaFP)
        {
            LastPhysicsEvent = PhysicsEvent.None;

            // 被抓取时位置由抓取者在 UpdateGrab 中设置
            if (action == null || Hurt == HurtState.Grabbed) return LastPhysicsEvent;

            FPVector3 delta;
            if (UnderForceMove)
            {
                delta = _forceMove.Step(deltaFP);
            }
            else
            {
                delta = NatureMove(deltaFP);
            }

            // 竖直只走速度模型：起跳设置速度，动作附加加速度与重力合成积分 (曲线位移的 z 分量不参与)
            if (action.VelocityZTriggered) motion.Launch(action.VelocityZ);

            // 水平惯性：动作在本帧设置的速度按朝向镜像为世界速度，之后与动画帧无关地持续移动
            if (action.PlanarVelocityTriggered)
            {
                FPVector3 v = action.PlanarVelocity;
                motion.SetPlanarVelocity(FacingLeft ? -v.x : v.x, v.y);
            }

            LastPhysicsEvent = motion.Step(delta, action.ZAcceleration, gravity, deltaFP, map);

            // 落地兜底：空中动作或击飞状态在地面上 (从未真正离地，不会产生 Landed 事件) 时视同落地
            if ((LastPhysicsEvent & PhysicsEvent.Landed) == 0 && motion.IsGrounded &&
                (action.CurrentAction.IsAirAction || Hurt == HurtState.Launch))
            {
                LastPhysicsEvent |= PhysicsEvent.Landed;
            }

            // 击飞触地：按落地速度衰减弹起 (无伤害，保持击飞并重播 hurt_inair)，弹不起则倒地
            if (Hurt == HurtState.Launch && (LastPhysicsEvent & PhysicsEvent.Landed) != 0)
            {
                FP bounce = motion.LastImpactSpeed * bounceRestitution;
                if (bounce > FP.Zero && bounce >= minBounceVelocity)
                {
                    motion.Launch(bounce);
                    LastPhysicsEvent = (LastPhysicsEvent & ~PhysicsEvent.Landed) | PhysicsEvent.Bounced;
                    PreorderHurtFlow(HurtActionIds.InAir, HurtActionIds.LandPriority);
                }
                else
                {
                    EnterLying();
                }
            }

            // 落地清除水平惯性 (击飞触地弹起不算落地，保留惯性；弹不起转倒地同样清除)
            if ((LastPhysicsEvent & PhysicsEvent.Landed) != 0) motion.ClearPlanarVelocity();

            action.OnPhysicsEvent(LastPhysicsEvent);
            return LastPhysicsEvent;
        }

        /// <summary>
        /// 自然水平位移：摇杆 8 方向分量 × 各轴速度 × 动作各轴移动接受率 + RootMotion (x 随朝向镜像)
        /// 横向与纵深各自独立计算后相加，斜向不归一化 (与原版一致)
        /// </summary>
        public FPVector3 NatureMove(FP deltaFP)
        {
            FPVector3 rootMotion = action.RootMotionMove;
            int dirX = 0, dirY = 0;
            if (input != null && !action.ControlLocked)
            {
                dirX = input.DirX;
                dirY = input.DirY;
            }
            FP rateX = action.MoveRateX * deltaFP;
            FP rateY = action.MoveRateY * deltaFP;
            FP facing = FacingLeft ? -FP.One : FP.One;

            return new FPVector3(
                moveSpeedX * rateX * dirX + rootMotion.x * facing,
                moveSpeedY * rateY * dirY + rootMotion.y,
                FP.Zero
            );
        }

        /// <summary>
        /// 设置强制外力位移 (世界空间，本地配置需先经 MoveInfo.ToWorld 按施力者朝向转换)
        /// </summary>
        public void SetForceMove(MoveInfo worldForce)
        {
            _forceMove.Start(worldForce);
        }

        public void ClearForceMove()
        {
            _forceMove.Stop();
        }

        #endregion

        #region 受击状态

        /// <summary>
        /// 进入僵直：覆盖剩余僵直时长；ticks <= 0 时不改变当前状态
        /// </summary>
        public void EnterStun(int ticks)
        {
            if (ticks <= 0) return;
            Hurt = HurtState.Stun;
            StunTicks = ticks;
            GrabbedBy = null;
            action?.Lock(true);
        }

        /// <summary>
        /// 进入击飞 (竖直速度由调用方通过 motion.Launch 设置)，持续到触地弹不起
        /// </summary>
        public void EnterLaunch()
        {
            Hurt = HurtState.Launch;
            StunTicks = 0;
            GrabbedBy = null;
            action?.Lock(false);
        }

        /// <summary>
        /// 被抓取：清除竖直速度、外力与吸附，位置交由抓取者每帧设置
        /// </summary>
        public void EnterGrabbed(CharacterObj grabber)
        {
            Hurt = HurtState.Grabbed;
            StunTicks = 0;
            GrabbedBy = grabber;
            motion.Launch(FP.Zero);
            motion.ClearPlanarVelocity();
            ClearForceMove();
            StopSuction();
            action?.Lock(true);
        }

        /// <summary>
        /// 倒地：播放 lie，播完起身 (击飞触地弹不起时进入，也可供直接击倒类效果调用)
        /// </summary>
        public void EnterLying()
        {
            Hurt = HurtState.Lying;
            StunTicks = 0;
            action?.Lock(false);
            PreorderHurtFlow(HurtActionIds.Lie, HurtActionIds.LandPriority);
        }

        /// <summary>
        /// 起身：播放 standup，期间无敌；不再受吸附等持续效果影响
        /// </summary>
        private void EnterStandup()
        {
            Hurt = HurtState.Standup;
            StopSuction();
            action?.Lock(false);
            PreorderHurtFlow(HurtActionIds.Standup);
        }

        /// <summary>
        /// 解除受击状态并回到默认动作 (调用方另有预约时以优先级更高者为准)
        /// </summary>
        private void ExitHurt()
        {
            Hurt = HurtState.None;
            StunTicks = 0;
            GrabbedBy = null;
            if (action == null) return;
            action.Unlock();
            PreorderHurtFlow(action.DefaultActionId);
        }

        /// <summary>
        /// 受击流程推进：当前受击动作播完时决定下一个动作
        /// - 击飞：hurt_toair -> hurt_inair，其余 (hurt_inair / hurt_fall / 自定义击飞动作) -> hurt_fall (循环到落地)
        /// - 倒地：lie 播完 -> 起身
        /// - 起身：standup 播完 -> 默认动作，并追加起身后无敌
        /// </summary>
        private void UpdateHurtFlow()
        {
            if (action == null || action.anim == null || !action.anim.IsFinished) return;
            string current = action.CurrentAction.id;

            switch (Hurt)
            {
                case HurtState.Launch:
                    PreorderHurtFlow(current == HurtActionIds.ToAir ? HurtActionIds.InAir : HurtActionIds.Fall);
                    break;
                case HurtState.Lying:
                    if (current == HurtActionIds.Lie) EnterStandup();
                    break;
                case HurtState.Standup:
                    // 起身动作或替身动作 (breakHurt) 播完：回到默认动作并追加起身后无敌
                    if (current == HurtActionIds.Standup || action.CurrentAction.breakHurt)
                    {
                        ExitHurt();
                        InvincibleTicks = GameConfig.SecondsToTicks(standupInvincibleSec);
                    }
                    break;
            }
        }

        /// <summary>
        /// 替身：进入 breakHurt 动作时解除当前受击 (被抓则脱离抓取者，清除僵直 / 击飞 / 倒地、外力与吸附)，
        /// 以起身状态播放该动作 (期间无敌)，播完与倒地起身一样追加起身后无敌
        /// </summary>
        private void BreakHurt()
        {
            // 抓取者在下一次 UpdateGrab 发现目标已不处于被抓状态，自动松手
            GrabbedBy = null;
            StunTicks = 0;
            ClearForceMove();
            StopSuction();
            Hurt = HurtState.Standup;
            action?.Lock(false);
        }

        /// <summary>
        /// 寿命到期时进入消失动作的预约优先级 (高于一切受击与强制动作)
        /// </summary>
        public const int DespawnPriority = 300;

        private void TickLifetime()
        {
            if (Spawn == null || LifeTicks <= 0 || --LifeTicks > 0) return;
            if (action == null || string.IsNullOrEmpty(Spawn.despawnActionId)) return;

            // 消失动作：解除受击，期间无敌，播完由 BattleWorld 回收
            Despawning = true;
            Hurt = HurtState.None;
            StunTicks = 0;
            ClearForceMove();
            StopSuction();
            GrabbedBy = null;
            ReleaseGrab();
            action.Unlock();
            action.PreorderActionByActionChangeInfo(HurtActionIds.ChangeTo(Spawn.despawnActionId, DespawnPriority), ForceDirection.Forward);
        }

        /// <summary>
        /// 生成物是否应在本帧回收 (BattleWorld 第 7 步检查)：
        /// - 寿命到期且没有消失动作；消失动作播完 (或未配置该动作)
        /// - Actor 生命值归零
        /// - Simple 动作链走完：当前动作播完、没有 autoNext、也不在等待落地
        /// </summary>
        public bool ShouldDespawn()
        {
            if (Spawn == null || action == null) return false;
            if (Despawning) return action.CurrentAction.id != Spawn.despawnActionId || action.anim == null || action.anim.IsFinished;
            if (LifeTicks == 0) return true;
            if (Spawn.type == SpawnType.Actor) return IsDead;

            ActionInfo cur = action.CurrentAction;
            return action.anim != null && action.anim.IsFinished && !cur.keepPlayingAnim &&
                   string.IsNullOrEmpty(cur.autoNextActionId) && cur.onLand.changeType == ActionChangeType.Keep;
        }

        /// <summary>
        /// 生成物复用前的身份设置 (BattleWorld 生成时调用，随后 AddCharacter 重置战斗状态)
        /// </summary>
        internal void SetupSpawned(CharacterObj owner, int sourceSkill, int lifeTicks)
        {
            Owner = owner;
            SourceSkill = sourceSkill;
            LifeTicks = lifeTicks > 0 ? lifeTicks : -1;
            Despawning = false;
            if (Spawn != null && Spawn.hp > FP.Zero) maxHp = Spawn.hp;
        }

        private void PreorderHurtFlow(string actionId, int priority = HurtActionIds.FlowPriority)
        {
            if (action == null || string.IsNullOrEmpty(actionId)) return;
            action.PreorderActionByActionChangeInfo(HurtActionIds.ChangeTo(actionId, priority), ForceDirection.Forward);
        }

        /// <summary>
        /// 扣除生命值 (霸体抵挡时同样结算)
        /// </summary>
        public void TakeDamage(FP damage)
        {
            if (damage <= FP.Zero) return;
            FP floor = infiniteHp ? FP.One : FP.Zero;
            Hp = FPMath.Max(floor, Hp - damage);
        }

        /// <summary>
        /// 进入战斗时重置战斗状态
        /// </summary>
        public void ResetCombatState()
        {
            Hp = maxHp;
            Hurt = HurtState.None;
            StunTicks = 0;
            InvincibleTicks = 0;
            GrabbedBy = null;
            GrabTarget = null;
            ClearForceMove();
            StopSuction();
            HitRecords.Clear();
            Skills?.Reset();
            Buffs.Clear();
        }

        #endregion

        #region 抓取与吸附 (BattleWorld 在全部角色物理之后统一更新，保证使用抓取者/吸附源本帧的最终位置)

        /// <summary>
        /// 抓住目标
        /// </summary>
        public void StartGrab(CharacterObj target, GrabInfo grab)
        {
            if (GrabTarget != null && GrabTarget != target) ReleaseGrab();
            GrabTarget = target;
            _grabAnchorName = string.IsNullOrEmpty(grab.anchor) ? GrabInfo.DefaultAnchor : grab.anchor;
            _grabRelease = grab.releaseActionChange;
            _grabAnchorLocal = FPVector3.zero;
            TryReadGrabAnchor(out _grabAnchorLocal);
            target.EnterGrabbed(this);
        }

        /// <summary>
        /// 维持抓取 (锚点即抓取)：
        /// 1. 目标已不处于被本角色抓取的状态 (如被直接击倒等外部效果改变状态) 则断开
        /// 2. 本逻辑帧推进了动画帧时，读取最后处理的动画帧上的抓取锚点：锚点消失即释放 (主动松手 / 动作播完 / 被打断)
        ///    本帧未推进动画帧时沿用上次锚点
        /// 3. 把目标锁到锚点
        /// </summary>
        public void UpdateGrab(MapLogic map)
        {
            if (GrabTarget == null) return;

            if (GrabTarget.Hurt != HurtState.Grabbed || GrabTarget.GrabbedBy != this)
            {
                GrabTarget = null;
                return;
            }

            FrameAnimator anim = action != null ? action.anim : null;
            if (anim != null && anim.TickToFrame > anim.TickFromFrame)
            {
                if (!TryReadGrabAnchor(out FPVector3 local))
                {
                    ReleaseGrab();
                    return;
                }
                _grabAnchorLocal = local;
            }

            FPVector3 pos = motion.Position + new FPVector3(FacingLeft ? -_grabAnchorLocal.x : _grabAnchorLocal.x, _grabAnchorLocal.y, _grabAnchorLocal.z);
            if (pos.z < FP.Zero) pos.z = FP.Zero;
            map?.ClampPlanar(ref pos);
            GrabTarget.motion.Position = pos;
        }

        /// <summary>
        /// 读取本逻辑帧最后处理的动画帧上的抓取锚点 (像素 -> 逻辑单位：x 前方, z 高度, 纵深为 0)
        /// </summary>
        private bool TryReadGrabAnchor(out FPVector3 local)
        {
            local = FPVector3.zero;
            FrameAnimator anim = action != null ? action.anim : null;
            if (anim == null) return false;

            int frame = anim.TickToFrame > anim.TickFromFrame ? anim.TickToFrame - 1 : anim.TickToFrame;
            if (!anim.TryGetAnchorAtFrame(frame, _grabAnchorName, out Vector2Int px)) return false;

            local = new FPVector3(GameConfig.PixelToUnit(px.x), FP.Zero, GameConfig.PixelToUnit(px.y));
            return true;
        }

        /// <summary>
        /// 非投掷方式解除抓取 (锚点消失 / 移出战场)：目标按 releaseActionChange 变化动作，在空中则转为击飞状态下落
        /// </summary>
        public void ReleaseGrab()
        {
            CharacterObj target = GrabTarget;
            GrabTarget = null;
            if (target != null && target.GrabbedBy == this) target.ReleaseFromGrab(_grabRelease);
        }

        private void ReleaseFromGrab(ActionChangeInfo releaseChange)
        {
            GrabbedBy = null;
            if (motion.Position.z > FP.Zero)
            {
                EnterLaunch();
            }
            else
            {
                ExitHurt();
            }
            action?.PreorderActionByActionChangeInfo(releaseChange, ForceDirection.Forward);
        }

        /// <summary>
        /// 开始被吸附 (覆盖当前吸附)
        /// </summary>
        public void StartSuction(CharacterObj source, SuctionInfo suction)
        {
            _suctionSource = source;
            _suction = suction;
            _suctionElapsed = FP.Zero;
        }

        public void StopSuction()
        {
            _suctionSource = null;
            _suctionElapsed = FP.Zero;
        }

        /// <summary>
        /// 吸附：每帧把水平位置向吸附源的核心点拉近 speed × dt，不越过核心点 (竖直方向不受影响)
        /// </summary>
        public void UpdateSuction(MapLogic map, FP deltaFP)
        {
            if (_suctionSource == null) return;
            if (Hurt == HurtState.Grabbed) return;
            if (_suctionElapsed >= _suction.inSec)
            {
                StopSuction();
                return;
            }
            _suctionElapsed += deltaFP;

            FPVector3 src = _suctionSource.motion.Position;
            FP cx = src.x + (_suctionSource.FacingLeft ? -_suction.center.x : _suction.center.x);
            FP cy = src.y + _suction.center.y;

            FPVector3 pos = motion.Position;
            FPVector2 toCenter = new FPVector2(cx - pos.x, cy - pos.y);
            FP dist = toCenter.magnitude;
            FP step = _suction.speed * deltaFP;

            if (dist <= step)
            {
                pos.x = cx;
                pos.y = cy;
            }
            else
            {
                FPVector2 move = toCenter * (step / dist);
                pos.x += move.x;
                pos.y += move.y;
            }

            map?.ClampPlanar(ref pos);
            motion.Position = pos;
        }

        #endregion

        #region 判定盒 (逐帧像素盒 + 动作阶段 tag 门控)

        /// <summary>
        /// 攻击盒在指定动画帧是否被当前动作的攻击阶段开启，并给出对应阶段 (同时命中多个阶段取优先级最高者)
        /// </summary>
        private bool TryGetAttackPhaseAtFrame(LogicBox box, int frame, out AttackBoxTurnOnInfo phase)
        {
            phase = default;
            bool found = false;
            AttackBoxTurnOnInfo[] phases = action.CurrentAction.attackPhase;
            if (phases == null) return false;

            foreach (AttackBoxTurnOnInfo info in phases)
            {
                if (!InRanges(info.frameRanges, frame) || !TagMatch(box.Tag, info.tag)) continue;
                if (!found || info.priority > phase.priority)
                {
                    phase = info;
                    found = true;
                }
            }
            return found;
        }

        private static bool InRanges(FrameRange[] ranges, int frame)
        {
            if (ranges == null) return false;
            foreach (FrameRange range in ranges)
            {
                if (frame >= range.x && frame <= range.y) return true;
            }
            return false;
        }

        /// <summary>
        /// 受击盒是否开启：动作未配置 defensePhase 时所有受击盒默认开启；
        /// 配置了 defensePhase 时仅在其开启区间内生效 (区间外即无敌)
        /// </summary>
        private bool TryGetDefensePhase(LogicBox box, out BeHitBoxTurnOnInfo phase)
        {
            phase = default;
            ActionInfo cur = action.CurrentAction;
            if (cur.defensePhase == null || cur.defensePhase.Length == 0) return true;

            bool found = false;
            foreach (BeHitBoxTurnOnInfo info in action.ActiveBeHitBoxInfo)
            {
                if (!TagMatch(box.Tag, info.tag)) continue;
                if (!found || info.priority > phase.priority)
                {
                    phase = info;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// 盒 Tag 为空时匹配任意开启的阶段，否则需在阶段 tag 列表中
        /// </summary>
        private static bool TagMatch(string boxTag, string[] phaseTags)
        {
            if (string.IsNullOrEmpty(boxTag)) return true;
            if (phaseTags == null) return false;
            foreach (string t in phaseTags)
            {
                if (t == boxTag) return true;
            }
            return false;
        }

        /// <summary>
        /// 判定当前逻辑帧是否命中目标角色 (纯定点数 AABB 检测，冒泡出最高优先级的盒子组合)
        /// 攻击方：逐个检测本逻辑帧处理过的每个动画帧 [TickFromFrame, TickToFrame) 上的攻击盒，跳过显示的帧也参与判定
        /// 受击方：使用当前姿态 (当前切片步) 的受击盒
        /// </summary>
        public bool CanAttackTargetNow(CharacterObj target, out HitResult hit)
        {
            hit = default;
            if (target == null || target == this || target.action == null || action == null) return false;
            if (action.anim == null || target.action.anim == null) return false;
            if (action.ActiveAttackBoxInfo.Count == 0) return false;

            // 无敌不产生命中；已死亡不再受击；简单生成物 (投射物) 没有受击
            if (!target.Hittable || target.IsDead || target.IsInvincible || target.action.CurrentArmor == ArmorLevel.Invincible) return false;

            List<LogicBox> hurtBoxes = target.action.anim.GetCurrentHurtBoxes();
            if (hurtBoxes == null || hurtBoxes.Count == 0) return false;

            bool found = false;
            for (int frame = action.anim.TickFromFrame; frame < action.anim.TickToFrame; frame++)
            {
                if (CheckHitAtFrame(target, frame, hurtBoxes, ref hit)) found = true;
            }
            return found;
        }

        /// <summary>
        /// 检测攻击方某一动画帧的攻击盒，若得到更高优先级的命中则写入 hit
        /// </summary>
        private bool CheckHitAtFrame(CharacterObj target, int frame, List<LogicBox> hurtBoxes, ref HitResult hit)
        {
            List<LogicBox> hitBoxes = action.anim.GetHitBoxesAtFrame(frame);
            if (hitBoxes == null || hitBoxes.Count == 0) return false;

            ActionInfo cur = action.CurrentAction;
            bool found = false;

            foreach (LogicBox hitBox in hitBoxes)
            {
                if (!hitBox.IsValid || !TryGetAttackPhaseAtFrame(hitBox, frame, out AttackBoxTurnOnInfo atkPhase)) continue;

                int phaseIdx = atkPhase.attackPhase;
                if (cur.attacks == null || phaseIdx < 0 || phaseIdx >= cur.attacks.Length) continue;

                // 倒地目标只能被扫地 (canHitLying) 攻击命中
                if (target.IsLying && !cur.attacks[phaseIdx].canHitLying) continue;

                int recIdx = IndexOfHitRecord(target, phaseIdx);
                if (recIdx >= 0 && !HitRecords[recIdx].CanHitNow) continue;

                FPBox atkWorld = hitBox.ToWorld(motion.Position, FacingLeft);

                foreach (LogicBox hurtBox in hurtBoxes)
                {
                    if (!hurtBox.IsValid || !target.TryGetDefensePhase(hurtBox, out BeHitBoxTurnOnInfo defPhase)) continue;

                    FPBox defWorld = hurtBox.ToWorld(target.motion.Position, target.FacingLeft);
                    if (!atkWorld.Overlaps(defWorld)) continue;

                    int priority = atkPhase.priority + defPhase.priority;
                    if (hit.Attacker != null && priority <= hit.Priority) continue;

                    found = true;
                    hit = new HitResult
                    {
                        Attacker = this,
                        Target = target,
                        AttackPhase = phaseIdx,
                        Attack = cur.attacks[phaseIdx],
                        Defense = defPhase,
                        HitPoint = atkWorld.IntersectionCenter(defWorld),
                        Priority = priority
                    };
                }
            }

            return found;
        }

        #endregion

        #region 命中记录

        /// <summary>
        /// 添加或更新命中记录 (使用定点数冷却)
        /// </summary>
        public void AddHitRecord(CharacterObj target, int attackPhase)
        {
            if (target == null || action == null) return;
            ActionInfo cur = action.CurrentAction;
            if (cur.attacks == null || attackPhase < 0 || attackPhase >= cur.attacks.Length) return;

            AttackInfo atk = cur.attacks[attackPhase];
            int recIdx = IndexOfHitRecord(target, attackPhase);
            if (recIdx < 0)
            {
                HitRecords.Add(new HitRecord(target, attackPhase, atk.canHitSameTarget - 1, atk.hitSameTargetDelay));
            }
            else
            {
                HitRecord rec = HitRecords[recIdx];
                rec.Cooldown = atk.hitSameTargetDelay;
                rec.CanHitTimes -= 1;
                HitRecords[recIdx] = rec;
            }
        }

        /// <summary>
        /// 查找对目标某段伤害的命中记录下标，不存在返回 -1
        /// </summary>
        public int IndexOfHitRecord(CharacterObj target, int phase)
        {
            if (target == null) return -1;
            for (int i = 0; i < HitRecords.Count; i++)
            {
                if (HitRecords[i].UniqueId == target.EntityId && HitRecords[i].Phase == phase)
                    return i;
            }
            return -1;
        }

        #endregion
    }
}
