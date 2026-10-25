using System;
using System.Collections.Generic;
using TrueSync;
using KiHan.Math;

namespace KiHan.Logic
{
    /// <summary>
    /// 战斗逻辑世界 (纯确定性逻辑帧调度)
    ///
    /// 每帧固定流程 (所有遍历均按 EntityId 升序)：
    /// 1. 输入：玩家灌入本帧 InputFrame；没有输入槽位的分身由大脑 (CloneBrain) 生成本帧输入
    /// 2. 动作：技能 CD / 生成物寿命倒计时；FrameAnimator 推进 RENDER_LOGIC_RATIO 个动画帧 (30fps 素材 / 30fps 同步 = 1)，
    ///    计算判定盒阶段、RootMotion、起跳/加速度，触发动作事件 (生成请求 / 表现事件)，收集打断与 autoNext 预约 (不立即切换)
    /// 3. 物理：外力/自然位移 + 重力 + 地面 + 边界墙，落地事件回馈动作；
    ///    之后统一更新抓取 (目标锁到抓取者锚点) 与吸附 (向吸附源核心点拉扯)，使用各自来源本帧的最终位置
    /// 4. 检测：定点数 AABB 收集全部命中 (只收集不生效，互殴结果与遍历顺序无关；无敌/倒地/不可受击过滤)
    /// 5. 结算：HitResolver —— 扣血、查克拉、霸体裁决、受击反应 (抓取/击飞/倒地/僵直)、平推与吸附、双方动作变化、打击事件
    /// 6. 裁决：主动打断、autoNext、落地、命中产生的全部动作预约按优先级当帧生效 (检测之后才切换，末帧攻击盒不会丢失)；
    ///    进入 / 离开技能动作时由 SkillController 结算段号、CD 与查克拉消耗
    /// 7. 生成与回收：先回收到期 / 死亡 / 动作链走完的生成物，再按请求顺序执行本帧的生成请求
    ///    (新实体编号从 FirstSpawnEntityId 起递增，下一帧开始参与流程)
    /// </summary>
    public class BattleWorld
    {
        /// <summary>
        /// 生成物的起始 EntityId (玩家使用输入槽位号，远小于该值)
        /// </summary>
        public const int FirstSpawnEntityId = 1000;

        public MapLogic Map { get; private set; }

        public uint FrameId { get; private set; }

        /// <summary>
        /// 生成物实例提供者 (未设置时忽略全部生成请求)
        /// </summary>
        public ISpawnProvider Spawner { get; set; }

        /// <summary>
        /// 下一个生成物的 EntityId
        /// </summary>
        public int NextEntityId { get; private set; } = FirstSpawnEntityId;

        /// <summary>
        /// 本帧打击事件 (表现层在 Tick 之后读取)
        /// </summary>
        public readonly List<HitEvent> HitEvents = new List<HitEvent>();

        /// <summary>
        /// 本帧动作事件中的表现部分 (特效 / 镜头)，表现层在 Tick 之后读取
        /// </summary>
        public readonly List<ViewEvent> ViewEvents = new List<ViewEvent>(16);

        /// <summary>
        /// 本帧新加入的生成物 (表现层为其创建或复用视图)
        /// </summary>
        public readonly List<CharacterObj> Spawned = new List<CharacterObj>(8);

        /// <summary>
        /// 本帧移出战场的生成物 EntityId (表现层隐藏其视图)
        /// </summary>
        public readonly List<int> Despawned = new List<int>(8);

        /// <summary>
        /// Buff 配置表 (id -> 配置)，加载阶段由各角色的 buffs.json 登记
        /// </summary>
        public readonly Dictionary<string, BuffInfo> BuffTable = new Dictionary<string, BuffInfo>();

        private readonly List<CharacterObj> _characters = new List<CharacterObj>();
        private readonly List<HitResult> _hits = new List<HitResult>();
        private readonly List<SpawnRequest> _spawnRequests = new List<SpawnRequest>(8);
        private readonly List<CharacterObj> _despawning = new List<CharacterObj>(8);

        public IReadOnlyList<CharacterObj> Characters => _characters;

        public BattleWorld(MapLogic map)
        {
            Map = map;
        }

        /// <summary>
        /// 加入角色 (按 EntityId 有序插入，ID 必须唯一)
        /// </summary>
        public void AddCharacter(CharacterObj cha)
        {
            if (cha == null) return;

            int idx = 0;
            while (idx < _characters.Count && _characters[idx].EntityId < cha.EntityId) idx++;
            if (idx < _characters.Count && _characters[idx].EntityId == cha.EntityId)
                throw new ArgumentException($"[BattleWorld] EntityId 重复: {cha.EntityId}");

            cha.ResetCombatState();
            cha.World = this;
            _characters.Insert(idx, cha);
        }

        public void RemoveCharacter(CharacterObj cha)
        {
            if (cha == null) return;
            // 移除的角色若正在抓取或被抓取，先解除绑定，避免对方永久锁定
            cha.ReleaseGrab();
            if (cha.GrabbedBy != null) cha.GrabbedBy.ReleaseGrab();
            _characters.Remove(cha);
            cha.World = null;

            // 以其为吸附来源的角色不再被拉扯
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].SuctionSource == cha) _characters[i].StopSuction();
            }
        }

        /// <summary>
        /// 动作事件 (由角色在第 2 步触发)：生成请求排队到第 7 步执行，Buff 立即添加，表现事件记入 ViewEvents
        /// </summary>
        public void OnActionEvent(CharacterObj source, in ActionEventInfo evt)
        {
            if (evt.type == ActionEventType.Buff)
            {
                if (BuffTable.TryGetValue(evt.asset, out BuffInfo buff)) AddBuff(source, buff);
                return;
            }

            FPVector3 point = source.EventPoint(evt.anchor, evt.offset);

            if (evt.type == ActionEventType.Spawn)
            {
                _spawnRequests.Add(new SpawnRequest
                {
                    Source = source,
                    SpawnId = evt.spawnId,
                    Position = point,
                    FacingLeft = source.FacingLeft,
                    SourceSkill = source.AttributedSkill()
                });
                return;
            }

            ViewEvents.Add(new ViewEvent
            {
                Type = evt.type,
                EntityId = source.EntityId,
                CharacterId = source.CharacterId,
                ActionSerial = source.action != null ? source.action.ActionSerial : 0,
                Asset = evt.asset,
                Anchor = evt.anchor,
                Offset = evt.offset,
                Bind = evt.bind,
                StopOnActionEnd = evt.stopOnActionEnd,
                Loop = evt.loop,
                FacingLeft = source.FacingLeft,
                Position = point
            });
        }

        /// <summary>
        /// 给角色添加一层 Buff，并通知表现层播放其循环特效
        /// </summary>
        public void AddBuff(CharacterObj target, BuffInfo buff)
        {
            int serial = target.Buffs.Add(buff);
            ViewEvents.Add(new ViewEvent
            {
                Type = ActionEventType.Buff,
                EntityId = target.EntityId,
                CharacterId = target.CharacterId,
                Asset = buff.effect,
                Anchor = buff.anchor,
                Offset = buff.offset,
                Bind = true,
                Loop = true,
                FacingLeft = target.FacingLeft,
                BuffSerial = serial,
                Position = target.motion.Position
            });
        }

        /// <summary>
        /// Buff 到期 (由 BuffController 调用)：通知表现层停止对应特效
        /// </summary>
        public void OnBuffRemoved(CharacterObj target, BuffInstance buff)
        {
            ViewEvents.Add(new ViewEvent
            {
                Type = ActionEventType.BuffRemoved,
                EntityId = target.EntityId,
                CharacterId = target.CharacterId,
                Asset = buff.Info.effect,
                BuffSerial = buff.Serial
            });
        }

        public void Tick(RoomFrame frame)
        {
            FP dt = GameConfig.LOGIC_TICK_TIME_FP;
            FrameId = frame != null ? frame.FrameId : FrameId + 1;
            HitEvents.Clear();
            ViewEvents.Clear();
            Spawned.Clear();
            Despawned.Clear();

            // 1. 输入
            for (int i = 0; i < _characters.Count; i++)
            {
                CharacterObj cha = _characters[i];
                if (cha.input == null) continue;

                InputFrame input = null;
                if (cha.InputSlot >= 0)
                {
                    if (frame != null && frame.InputFrames != null) frame.InputFrames.TryGetValue((byte)cha.InputSlot, out input);
                }
                else if (cha.Brain != null)
                {
                    input = cha.Brain.Think(cha);
                }
                cha.input.FeedInput(input, FrameId);
            }

            // 2. 动作
            for (int i = 0; i < _characters.Count; i++)
            {
                _characters[i].Tick(dt);
            }

            // 3. 物理
            for (int i = 0; i < _characters.Count; i++)
            {
                _characters[i].PhysicsTick(Map, dt);
            }

            // 3.1 抓取与吸附 (依赖抓取者/吸附源本帧物理后的位置)
            for (int i = 0; i < _characters.Count; i++)
            {
                _characters[i].UpdateGrab(Map);
            }
            for (int i = 0; i < _characters.Count; i++)
            {
                _characters[i].UpdateSuction(Map, dt);
            }

            // 4. 检测
            _hits.Clear();
            for (int a = 0; a < _characters.Count; a++)
            {
                CharacterObj attacker = _characters[a];
                for (int t = 0; t < _characters.Count; t++)
                {
                    CharacterObj target = _characters[t];
                    if (a == t || attacker.TeamId == target.TeamId) continue;
                    if (attacker.CanAttackTargetNow(target, out HitResult hit)) _hits.Add(hit);
                }
            }

            // 5. 结算
            for (int i = 0; i < _hits.Count; i++)
            {
                HitEvents.Add(HitResolver.Resolve(_hits[i], FrameId));
            }

            // 6. 裁决
            for (int i = 0; i < _characters.Count; i++)
            {
                _characters[i].action?.FlushPreorders();
            }

            // 7. 生成与回收 (回收的实例在本帧生成之后才归还，同一帧内不会被复用，表现层可区分新旧实体)
            DespawnFinished();
            ExecuteSpawnRequests();
            for (int i = 0; i < _despawning.Count; i++)
            {
                Spawner?.Release(_despawning[i]);
            }
            _despawning.Clear();
        }

        /// <summary>
        /// 回收应当离场的生成物 (按 EntityId 降序检查，移除不影响尚未检查的元素)
        /// </summary>
        private void DespawnFinished()
        {
            for (int i = _characters.Count - 1; i >= 0; i--)
            {
                CharacterObj cha = _characters[i];
                if (!cha.ShouldDespawn()) continue;

                RemoveCharacter(cha);
                Despawned.Add(cha.EntityId);
                _despawning.Add(cha);
            }
        }

        private void ExecuteSpawnRequests()
        {
            for (int i = 0; i < _spawnRequests.Count; i++)
            {
                SpawnRequest req = _spawnRequests[i];
                CharacterObj entity = Spawner?.Acquire(req.Source, req.SpawnId);
                if (entity == null || entity.Spawn == null) continue;

                SpawnInfo info = entity.Spawn;
                entity.EntityId = NextEntityId++;
                entity.TeamId = req.Source.TeamId;
                entity.InputSlot = -1;
                entity.CharacterId = req.Source.CharacterId;
                entity.SetupSpawned(req.Source.RootOwner, req.SourceSkill, info.lifeTicks);
                entity.FacingLeft = req.FacingLeft;
                entity.motion.Reset(req.Position);
                entity.input?.Reset();
                entity.Brain?.Setup(info.brain, info.script);
                entity.action.ResetTo(info.initialActionId);

                AddCharacter(entity);
                Spawned.Add(entity);
            }
            _spawnRequests.Clear();
        }
    }
}
