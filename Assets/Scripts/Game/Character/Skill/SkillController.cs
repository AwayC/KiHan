using System;
using System.Collections.Generic;

namespace KiHan.Logic
{
    /// <summary>
    /// 技能资源控制器 (每个玩家角色一个，纯确定性整数逻辑帧)
    ///
    /// 职责：
    /// 1. 技能准入：入口动作只在 CD 结束、没有换段、查克拉足够、满足受击条件时允许进入；换上的下一段动作只在换段期间允许进入
    ///    (ActionController 主动打断时裁决)
    /// 2. 换段：动作指令 skillStage 把按键换成下一段动作并开始接段计时，超时恢复按键并开始 CD (返还 cdReturn)
    /// 3. CD：释放入口动作或换上的下一段动作时立即开始计算完整 CD；换段期间 CD 清零
    /// 4. 查克拉：技能刷新 (CD 结束) 后可获得一次，释放该技能期间的动作或其生成物命中时 +1，之后要等下次刷新
    /// </summary>
    public class SkillController
    {
        public const int DefaultChakraMax = 4;

        private struct ActionRef
        {
            public int Skill;

            /// <summary>
            /// true：技能入口动作；false：skillStage 换上的下一段动作
            /// </summary>
            public bool Entry;
        }

        private SkillInfo[] _skills = Array.Empty<SkillInfo>();
        private readonly Dictionary<string, ActionRef> _actionMap = new Dictionary<string, ActionRef>();
        private readonly List<string> _stageIcons = new List<string>();

        private int[] _cooldown = Array.Empty<int>();
        private int[] _window = Array.Empty<int>();
        private int[] _windowTotal = Array.Empty<int>();
        private int[] _cdReturn = Array.Empty<int>();
        private string[] _stageAction = Array.Empty<string>();
        private string[] _stageIcon = Array.Empty<string>();
        private bool[] _chakraReady = Array.Empty<bool>();

        /// <summary>
        /// 正在释放的技能下标，-1 表示没有：进入入口动作或下一段动作时设置，之后由落地 / 指令跳转 / 派生进入的动作沿用，
        /// 回到默认动作或进入受击时结束 (查克拉与生成物来源按此归属)
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
        /// 接下一段的剩余时限 (逻辑帧)，0 表示没有换段
        /// </summary>
        public int GetWindow(int index) => _window[index];

        /// <summary>
        /// 本次换段的接段时限总长 (逻辑帧)，没有换段时为 0
        /// </summary>
        public int GetWindowTotal(int index) => _windowTotal[index];

        /// <summary>
        /// 换段期间按键对应的下一段动作，没有换段时为空串
        /// </summary>
        public string GetStageAction(int index) => _stageAction[index];

        /// <summary>
        /// 按键当前应显示的图标：换段期间该段配置了图标用该段的，否则用技能图标
        /// </summary>
        public string GetIcon(int index)
        {
            return _window[index] > 0 && !string.IsNullOrEmpty(_stageIcon[index]) ? _stageIcon[index] : _skills[index].icon;
        }

        /// <summary>
        /// 全部 skillStage 指令配置的图标 (Setup 时收集，供表现层预载)
        /// </summary>
        public IReadOnlyList<string> StageIcons => _stageIcons;

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

        /// <summary>
        /// 按技能 id 查找技能下标，没有返回 -1
        /// </summary>
        public int IndexOfSkill(string skillId)
        {
            for (int i = 0; i < _skills.Length; i++)
            {
                if (_skills[i].id == skillId) return i;
            }
            return -1;
        }

        /// <summary>
        /// 装载技能：登记入口动作，并扫描动作库中的 skillStage 指令登记各技能的下一段动作 (不在换段期间时被准入拦截)
        /// </summary>
        public void Setup(SkillInfo[] skills, IReadOnlyList<ActionInfo> actions, int chakraMax, int chakraStart = 0)
        {
            _skills = skills ?? Array.Empty<SkillInfo>();
            ChakraMax = chakraMax > 0 ? chakraMax : DefaultChakraMax;
            ChakraStart = System.Math.Max(0, System.Math.Min(chakraStart, ChakraMax));

            _actionMap.Clear();
            _stageIcons.Clear();
            for (int s = 0; s < _skills.Length; s++)
            {
                Register(_skills[s].actionId, s, true);
            }
            if (actions != null)
            {
                for (int a = 0; a < actions.Count; a++)
                {
                    ActionScript[] scripts = actions[a].scripts;
                    if (scripts == null) continue;
                    foreach (ActionScript script in scripts)
                    {
                        if (script.op != ScriptOp.SkillStage) continue;
                        int s = IndexOfSkill(script.skill);
                        if (s < 0)
                        {
                            UnityEngine.Debug.LogError($"[SkillController] 动作 {actions[a].id} 的 skillStage 指令引用了不存在的技能 {script.skill}");
                            continue;
                        }
                        Register(script.action, s, false);
                        if (!string.IsNullOrEmpty(script.icon) && !_stageIcons.Contains(script.icon)) _stageIcons.Add(script.icon);
                    }
                }
            }

            _cooldown = new int[_skills.Length];
            _window = new int[_skills.Length];
            _windowTotal = new int[_skills.Length];
            _cdReturn = new int[_skills.Length];
            _stageAction = new string[_skills.Length];
            _stageIcon = new string[_skills.Length];
            _chakraReady = new bool[_skills.Length];
            Reset();
        }

        private void Register(string actionId, int skill, bool entry)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            if (_actionMap.TryGetValue(actionId, out ActionRef existing))
            {
                // 同一技能的多条 skillStage 换上同一个动作 (如不同分支都接到同一段) 是允许的
                if (existing.Skill == skill && existing.Entry == entry) return;
                UnityEngine.Debug.LogError($"[SkillController] 动作 {actionId} 同时是多个技能的入口或下一段");
                return;
            }
            _actionMap.Add(actionId, new ActionRef { Skill = skill, Entry = entry });
        }

        /// <summary>
        /// 开局重置：全部技能就绪、没有换段、可获得查克拉，查克拉回到开局值 (ChakraStart)
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _skills.Length; i++)
            {
                _cooldown[i] = 0;
                ClearStage(i);
                _chakraReady[i] = true;
            }
            CastingSkill = -1;
            Chakra = ChakraStart;
        }

        /// <summary>
        /// 训练场无限火力：清空全部技能 CD、刷新查克拉可获得状态并回满查克拉 (换段与接段时限保留)
        /// </summary>
        public void Refill()
        {
            for (int i = 0; i < _skills.Length; i++)
            {
                _cooldown[i] = 0;
                _chakraReady[i] = true;
            }
            Chakra = ChakraMax;
        }

        /// <summary>
        /// 逻辑帧推进：接段时限 (释放期间同样倒数) 到期恢复按键并开始 CD；CD 倒计时，CD 结束刷新查克拉可获得状态
        /// </summary>
        public void Tick()
        {
            for (int s = 0; s < _skills.Length; s++)
            {
                if (_window[s] > 0)
                {
                    if (--_window[s] == 0) StartCooldown(s, _cdReturn[s]);
                }
                else if (_cooldown[s] > 0 && --_cooldown[s] == 0)
                {
                    _chakraReady[s] = true;
                }
            }
        }

        /// <summary>
        /// 技能准入：不属于任何技能的动作一律放行；入口动作需 CD 结束、没有换段、查克拉足够、满足受击条件；
        /// 下一段动作只在其技能换段到它时放行
        /// </summary>
        public bool CanEnter(string actionId, HurtState hurt)
        {
            if (string.IsNullOrEmpty(actionId) || !_actionMap.TryGetValue(actionId, out ActionRef r)) return true;

            int s = r.Skill;
            if (!r.Entry) return _window[s] > 0 && _stageAction[s] == actionId;

            SkillInfo skill = _skills[s];
            if (_cooldown[s] > 0 || _window[s] > 0) return false;
            if (Chakra < skill.chakraCost) return false;
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
        /// 动作切换：进入入口动作时扣除查克拉并开始完整 CD；进入换上的下一段动作时恢复按键并开始完整 CD
        /// (该动作进入帧的 skillStage 指令随后可再次换段)；其他动作沿用正在释放的技能，neutral 时结束释放
        /// </summary>
        /// <param name="neutral">进入的是默认动作或受击动作</param>
        /// <returns>本次进入的是某技能的入口动作时返回该技能下标 (调用方据此加技能的 Buff)，否则 -1</returns>
        public int OnActionChanged(string toActionId, bool neutral)
        {
            if (!string.IsNullOrEmpty(toActionId) && _actionMap.TryGetValue(toActionId, out ActionRef to))
            {
                int s = to.Skill;
                if (to.Entry)
                {
                    CastingSkill = s;
                    Chakra = System.Math.Max(0, Chakra - _skills[s].chakraCost);
                    StartCooldown(s, 0);
                    return s;
                }
                if (_window[s] > 0 && _stageAction[s] == toActionId)
                {
                    CastingSkill = s;
                    StartCooldown(s, 0);
                    return -1;
                }
            }
            if (neutral) CastingSkill = -1;
            return -1;
        }

        /// <summary>
        /// skillStage 指令：技能的按键换成 actionId，windowTicks 内可释放；期间 CD 清零，超时开始 CD 并返还 cdReturnTicks
        /// </summary>
        public void EnterStage(string skillId, string actionId, int windowTicks, int cdReturnTicks, string icon)
        {
            int s = IndexOfSkill(skillId);
            if (s < 0) return;
            if (windowTicks <= 0)
            {
                StartCooldown(s, cdReturnTicks);
                return;
            }
            _cooldown[s] = 0;
            _window[s] = windowTicks;
            _windowTotal[s] = windowTicks;
            _cdReturn[s] = cdReturnTicks;
            _stageAction[s] = actionId ?? string.Empty;
            _stageIcon[s] = icon ?? string.Empty;
        }

        /// <summary>
        /// skillEnd 指令：立即恢复按键并开始 CD，返还 cdReturnTicks
        /// </summary>
        public void EndStage(string skillId, int cdReturnTicks)
        {
            int s = IndexOfSkill(skillId);
            if (s >= 0) StartCooldown(s, cdReturnTicks);
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

        private void StartCooldown(int s, int refundTicks)
        {
            ClearStage(s);
            _cooldown[s] = System.Math.Max(0, _skills[s].cooldownTicks - refundTicks);
            if (_cooldown[s] == 0) _chakraReady[s] = true;
        }

        private void ClearStage(int s)
        {
            _window[s] = 0;
            _windowTotal[s] = 0;
            _cdReturn[s] = 0;
            _stageAction[s] = string.Empty;
            _stageIcon[s] = string.Empty;
        }
    }
}
