using System;
using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 技能资源控制器 (每个玩家角色一个，纯确定性整数逻辑帧)
    ///
    /// 职责：
    /// 1. 技能准入：入口动作只在 CD 结束、轮到该段、查克拉足够、满足受击条件时允许进入 (ActionController 主动打断时裁决)
    /// 2. 多段流转：动作切换时识别进入 / 离开技能段，维护下一段与接段时限
    /// 3. CD：释放最后一段 (进入其入口动作) 时立即开始计算；接段超时开始计算并返还 cdReturn
    /// 4. 查克拉：技能刷新 (CD 结束) 后可获得一次，该技能任一段或其生成物命中时 +1，之后要等下次刷新
    /// </summary>
    public class SkillController
    {
        public const int DefaultChakraMax = 4;

        private struct ActionRef
        {
            public int Skill;
            public int Stage;
            public bool Entry;
        }

        private SkillInfo[] _skills = Array.Empty<SkillInfo>();
        private readonly Dictionary<string, ActionRef> _actionMap = new Dictionary<string, ActionRef>();

        private int[] _cooldown = Array.Empty<int>();
        private int[] _nextStage = Array.Empty<int>();
        private int[] _window = Array.Empty<int>();
        private bool[] _chakraReady = Array.Empty<bool>();

        /// <summary>
        /// 正在释放的技能下标 (当前动作属于该技能的某一段)，-1 表示没有
        /// </summary>
        public int CastingSkill { get; private set; } = -1;

        public int Chakra { get; private set; }
        public int ChakraMax { get; private set; } = DefaultChakraMax;

        /// <summary>
        /// 开局 (Reset) 时的查克拉
        /// </summary>
        public int ChakraStart { get; private set; }

        public int Count => _skills.Length;

        public SkillInfo GetSkill(int index) => _skills[index];

        /// <summary>
        /// 剩余 CD (逻辑帧)
        /// </summary>
        public int GetCooldown(int index) => _cooldown[index];

        /// <summary>
        /// 下一次可以释放的段 (0 为第一段)
        /// </summary>
        public int GetNextStage(int index) => _nextStage[index];

        /// <summary>
        /// 接下一段的剩余时限 (逻辑帧)，0 表示不在接段窗口中
        /// </summary>
        public int GetWindow(int index) => _window[index];

        /// <summary>
        /// 该技能本轮是否还能获得查克拉
        /// </summary>
        public bool IsChakraReady(int index) => _chakraReady[index];

        /// <summary>
        /// 按 UI 按键查找技能下标，没有返回 -1
        /// </summary>
        public int IndexOfButton(CommandKey button)
        {
            for (int i = 0; i < _skills.Length; i++)
            {
                if (_skills[i].button == button) return i;
            }
            return -1;
        }

        public void Setup(SkillInfo[] skills, int chakraMax, int chakraStart = 0)
        {
            _skills = skills ?? Array.Empty<SkillInfo>();
            ChakraMax = chakraMax > 0 ? chakraMax : DefaultChakraMax;
            ChakraStart = System.Math.Max(0, System.Math.Min(chakraStart, ChakraMax));

            _actionMap.Clear();
            for (int s = 0; s < _skills.Length; s++)
            {
                SkillStageInfo[] stages = _skills[s].stages;
                for (int k = 0; k < stages.Length; k++)
                {
                    Register(stages[k].actionId, s, k, true);
                    foreach (string follow in stages[k].followActions) Register(follow, s, k, false);
                }
            }

            _cooldown = new int[_skills.Length];
            _nextStage = new int[_skills.Length];
            _window = new int[_skills.Length];
            _chakraReady = new bool[_skills.Length];
            Reset();
        }

        private void Register(string actionId, int skill, int stage, bool entry)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            if (_actionMap.ContainsKey(actionId))
            {
                UnityEngine.Debug.LogError($"[SkillController] 动作 {actionId} 同时属于多个技能段");
                return;
            }
            _actionMap.Add(actionId, new ActionRef { Skill = skill, Stage = stage, Entry = entry });
        }

        /// <summary>
        /// 开局重置：全部技能就绪、可获得查克拉，查克拉回到开局值 (ChakraStart)
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _skills.Length; i++)
            {
                _cooldown[i] = 0;
                _nextStage[i] = 0;
                _window[i] = 0;
                _chakraReady[i] = true;
            }
            CastingSkill = -1;
            Chakra = ChakraStart;
        }

        /// <summary>
        /// 逻辑帧推进：接段时限 (不在释放该技能时才计时) 与 CD 倒计时，CD 结束刷新查克拉可获得状态
        /// </summary>
        public void Tick()
        {
            for (int s = 0; s < _skills.Length; s++)
            {
                if (_window[s] > 0)
                {
                    if (CastingSkill != s && --_window[s] == 0)
                    {
                        StartCooldown(s, _skills[s].stages[_nextStage[s] - 1].cdReturnTicks);
                    }
                }
                else if (_cooldown[s] > 0 && --_cooldown[s] == 0)
                {
                    _chakraReady[s] = true;
                }
            }
        }

        /// <summary>
        /// 动作所属的技能下标 (入口或后续动作)，不属于任何技能返回 -1
        /// </summary>
        public int SkillIndexOfAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && _actionMap.TryGetValue(actionId, out ActionRef r) ? r.Skill : -1;
        }

        /// <summary>
        /// 技能准入：非技能入口动作一律放行
        /// </summary>
        public bool CanEnter(string actionId, HurtState hurt)
        {
            if (string.IsNullOrEmpty(actionId) || !_actionMap.TryGetValue(actionId, out ActionRef r) || !r.Entry) return true;

            SkillInfo skill = _skills[r.Skill];
            if (_cooldown[r.Skill] > 0 || r.Stage != _nextStage[r.Skill]) return false;
            if (r.Stage == 0 && Chakra < skill.chakraCost) return false;
            if (skill.requireHurt && !IsBreakableHurt(hurt)) return false;
            return true;
        }

        /// <summary>
        /// 可被替身打断的受击状态 (起身期间已无敌，不可使用)
        /// </summary>
        public static bool IsBreakableHurt(HurtState hurt)
        {
            return hurt == HurtState.Stun || hurt == HurtState.Launch || hurt == HurtState.Grabbed || hurt == HurtState.Lying;
        }

        /// <summary>
        /// 动作切换：离开正在释放的技能时结算 (未放完最后一段则进入接段窗口)，进入入口动作时推进段号并扣除查克拉，
        /// 进入最后一段时立即开始 CD
        /// </summary>
        public void OnActionChanged(string toActionId)
        {
            bool hasTo = _actionMap.TryGetValue(toActionId ?? string.Empty, out ActionRef to);

            if (CastingSkill >= 0 && (!hasTo || to.Skill != CastingSkill))
            {
                FinishCasting(CastingSkill);
                CastingSkill = -1;
            }

            if (hasTo && to.Entry)
            {
                CastingSkill = to.Skill;
                if (to.Stage == 0) Chakra = System.Math.Max(0, Chakra - _skills[to.Skill].chakraCost);
                if (to.Stage >= _skills[to.Skill].stages.Length - 1)
                {
                    StartCooldown(to.Skill, 0);
                }
                else
                {
                    _nextStage[to.Skill] = to.Stage + 1;
                    _window[to.Skill] = 0;
                }
            }
        }

        /// <summary>
        /// 命中时尝试获得查克拉：该技能本轮未获得过时 +1 (上限 ChakraMax)
        /// </summary>
        public bool TryGainChakra(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= _skills.Length || !_chakraReady[skillIndex]) return false;
            _chakraReady[skillIndex] = false;
            Chakra = System.Math.Min(ChakraMax, Chakra + 1);
            return true;
        }

        /// <summary>
        /// 离开技能：最后一段已在进入时开始 CD (段号已回到 0)；否则打开接段窗口，没有窗口则直接开始 CD 并返还 cdReturn
        /// </summary>
        private void FinishCasting(int s)
        {
            int next = _nextStage[s];
            if (next == 0) return;

            SkillStageInfo[] stages = _skills[s].stages;
            _window[s] = stages[next - 1].windowTicks;
            if (_window[s] <= 0) StartCooldown(s, stages[next - 1].cdReturnTicks);
        }

        private void StartCooldown(int s, int refundTicks)
        {
            _nextStage[s] = 0;
            _window[s] = 0;
            _cooldown[s] = System.Math.Max(0, _skills[s].cooldownTicks - refundTicks);
            if (_cooldown[s] == 0) _chakraReady[s] = true;
        }
    }
}
