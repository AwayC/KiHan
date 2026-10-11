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
    /// 1. 输入：灌入本帧 InputFrame
    /// 2. 动作：FrameAnimator 推进 RENDER_LOGIC_RATIO 个动画帧 (30fps 素材 / 30fps 同步 = 1)，计算判定盒阶段、RootMotion、起跳/加速度，收集打断与 autoNext 预约 (不立即切换)
    /// 3. 物理：外力/自然位移 + 重力 + 地面 + 边界墙，落地事件回馈动作；
    ///    之后统一更新抓取 (目标锁到抓取者锚点) 与吸附 (向吸附源核心点拉扯)，使用各自来源本帧的最终位置
    /// 4. 检测：定点数 AABB 收集全部命中 (只收集不生效，互殴结果与遍历顺序无关；无敌/倒地过滤)
    /// 5. 结算：HitResolver —— 扣血、霸体裁决、受击反应 (抓取/击飞/倒地/僵直)、平推与吸附、双方动作变化、打击事件
    /// 6. 裁决：主动打断、autoNext、落地、命中产生的全部动作预约按优先级当帧生效 (检测之后才切换，末帧攻击盒不会丢失)
    /// </summary>
    public class BattleWorld
    {
        public MapLogic Map { get; private set; }

        public uint FrameId { get; private set; }

        /// <summary>
        /// 本帧打击事件 (表现层在 Tick 之后读取)
        /// </summary>
        public readonly List<HitEvent> HitEvents = new List<HitEvent>();

        private readonly List<CharacterObj> _characters = new List<CharacterObj>();
        private readonly List<HitResult> _hits = new List<HitResult>();

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
            _characters.Insert(idx, cha);
        }

        public void RemoveCharacter(CharacterObj cha)
        {
            if (cha == null) return;
            // 移除的角色若正在抓取或被抓取，先解除绑定，避免对方永久锁定
            cha.ReleaseGrab();
            if (cha.GrabbedBy != null) cha.GrabbedBy.ReleaseGrab();
            _characters.Remove(cha);
        }

        public void Tick(RoomFrame frame)
        {
            FP dt = GameConfig.LOGIC_TICK_TIME_FP;
            FrameId = frame != null ? frame.FrameId : FrameId + 1;
            HitEvents.Clear();

            // 1. 输入
            for (int i = 0; i < _characters.Count; i++)
            {
                CharacterObj cha = _characters[i];
                if (cha.input == null) continue;

                InputFrame input = null;
                if (cha.InputSlot >= 0 && frame != null && frame.InputFrames != null)
                {
                    frame.InputFrames.TryGetValue((byte)cha.InputSlot, out input);
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
        }
    }
}
