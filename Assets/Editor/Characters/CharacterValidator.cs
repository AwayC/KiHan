using System;
using System.Collections.Generic;
using KiHan.Config;
using KiHan.Logic;
using KiHan.View.Audio;
using UnityEngine;
using UnityEngine.Playables;

namespace KiHan.Editor
{
    /// <summary>
    /// 角色包校验：身份信息、配置完整性与交叉引用 (动作 / 技能 / 生成物 / 动作事件 / 动画 / 包内资源 key)
    /// 菜单 "KiHan/角色/校验全部角色包"、清单面板的"校验"按钮与 Unity 环境测试共用
    /// </summary>
    public static class CharacterValidator
    {
        public static List<string> Validate(CharacterManifest manifest)
        {
            var problems = new List<string>();
            if (manifest.id <= 0) problems.Add("角色 ID 未填写");
            if (string.IsNullOrEmpty(manifest.displayName)) problems.Add("角色名未填写");
            if (manifest.headshot == null) problems.Add("头像未设置");
            if (!TrueSync.FP.TryParse(manifest.critRate, out TrueSync.FP critRate) || critRate < TrueSync.FP.Zero || critRate > TrueSync.FP.One)
                problems.Add($"暴击率 \"{manifest.critRate}\" 应为 0~1 的数字");
            if (!TrueSync.FP.TryParse(manifest.critMultiplier, out TrueSync.FP critMultiplier) || critMultiplier < TrueSync.FP.One)
                problems.Add($"暴击倍率 \"{manifest.critMultiplier}\" 应为不小于 1 的数字");
            if (manifest.actions == null)
            {
                problems.Add("缺少 Config/actions.json");
                return problems;
            }
            if (manifest.animations.Exists(a => a == null)) problems.Add("动画列表中有丢失的资源 (请刷新角色包)");

            foreach (CharacterManifest other in CharacterPackages.FindManifests())
            {
                if (other != manifest && other.id == manifest.id) problems.Add($"角色 ID {manifest.id} 与 {CharacterPackages.PackageRoot(other)} 重复");
            }

            CharacterTemplate template = CharacterFactory.BuildTemplate(manifest);
            problems.AddRange(ValidateTemplate(template));
            return problems;
        }

        /// <summary>
        /// 角色配置交叉引用：动作的 animKey / autoNext / 命中动作变化目标、技能段 (入口与后续) 动作、生成物初始与消失动作、
        /// 动作事件引用的生成物与特效资源、技能图标都必须存在；默认动作与受击动作齐全
        /// </summary>
        public static List<string> ValidateTemplate(CharacterTemplate template)
        {
            var problems = new List<string>();
            if (template == null)
            {
                problems.Add("模板加载失败");
                return problems;
            }

            CharacterManifest manifest = template.Manifest;
            var actionIds = new HashSet<string>();
            foreach (ActionInfo a in template.Actions) actionIds.Add(a.id);
            var animKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AnimationFrameData clip in template.Anims) animKeys.Add(clip.name);

            if (!actionIds.Contains(CharacterFactory.DefaultActionId)) problems.Add($"缺少默认动作 {CharacterFactory.DefaultActionId}");
            List<string> missingHurt = HurtActionIds.FindMissing(template.Actions);
            if (missingHurt.Count > 0) problems.Add($"缺少受击动作: {string.Join(", ", missingHurt)}");

            void RequireAction(string id, string where)
            {
                if (!string.IsNullOrEmpty(id) && !actionIds.Contains(id)) problems.Add($"{where} 引用了不存在的动作 {id}");
            }

            var skillIds = new HashSet<string>();
            var skillEntries = new HashSet<string>();
            foreach (SkillInfo skill in template.Skills)
            {
                skillIds.Add(skill.id);
                if (!string.IsNullOrEmpty(skill.actionId)) skillEntries.Add(skill.actionId);
            }

            void RequireSkill(string id, string where)
            {
                if (string.IsNullOrEmpty(id)) problems.Add($"{where} 没有配置技能 skill");
                else if (!skillIds.Contains(id)) problems.Add($"{where} 引用了不存在的技能 {id}");
            }

            void RequireIcon(string key, string where)
            {
                if (!string.IsNullOrEmpty(key) && global::View.UI.SkillIconCache.Load(key, manifest) == null) problems.Add($"{where} 的图标不存在: {key}");
            }

            void RequireEffect(string key, string where)
            {
                if (string.IsNullOrEmpty(key)) return;
                bool packaged = manifest != null && manifest.TryGetPrefab(key, out _, out _);
                if (!packaged && Resources.Load<GameObject>(key) == null) problems.Add($"{where} 的特效资源不存在: {key}");
            }

            void RequireSound(string key, string where)
            {
                if (string.IsNullOrEmpty(key)) return;
                bool packaged = manifest != null && (manifest.FindResource<SoundCue>(key) != null || manifest.FindResource<AudioClip>(key) != null);
                if (!packaged && Resources.Load<SoundCue>(key) == null && Resources.Load<AudioClip>(key) == null) problems.Add($"{where} 的音效资源不存在: {key}");
            }

            void RequireCinematic(string key, string where)
            {
                if (string.IsNullOrEmpty(key)) return;
                GameObject prefab = manifest != null ? manifest.FindResource<GameObject>(key) : null;
                if (prefab == null) prefab = Resources.Load<GameObject>(key);
                if (prefab == null)
                {
                    problems.Add($"{where} 的演出资源不存在: {key}");
                    return;
                }
                PlayableDirector director = prefab.GetComponent<PlayableDirector>();
                if (director == null || director.playableAsset == null) problems.Add($"{where} 的演出预制体缺少 PlayableDirector 或 Timeline: {key}");
            }

            void RequireBuff(string id, string where)
            {
                if (string.IsNullOrEmpty(id)) problems.Add($"{where} 没有配置 buff");
                else if (!template.Buffs.ContainsKey(id) && !SecretScrollRegistry.HasBuff(id)) problems.Add($"{where} 引用了不存在的 Buff {id} (buffs.json 或秘卷表)");
            }

            void ValidateScripts(ActionInfo a)
            {
                if (a.scripts == null) return;
                if (a.scripts.Length > ActionController.MaxScriptsPerAction)
                    problems.Add($"动作 {a.id} 的指令超过 {ActionController.MaxScriptsPerAction} 条");
                for (int i = 0; i < a.scripts.Length; i++)
                {
                    ActionScript s = a.scripts[i];
                    string where = $"动作 {a.id} 的 scripts[{i}]";
                    if (s.op == ScriptOp.None) problems.Add($"{where} 无法识别的 op");
                    if (s.on == ScriptTrigger.Input && (s.input.press == null || s.input.press.Length == 0))
                        problems.Add($"{where} 是 input 触发但没有配置按键");
                    else if (s.on == ScriptTrigger.Input && Array.IndexOf(s.input.press, CommandKey.None) >= 0)
                        problems.Add($"{where} 的按键无法识别 (摇杆键不能用于 input)");
                    switch (s.op)
                    {
                        case ScriptOp.Goto:
                            if (s.on == ScriptTrigger.Exit) problems.Add($"{where} 在 exit 触发中使用了 goto");
                            if (string.IsNullOrEmpty(s.action)) problems.Add($"{where} 的 goto 没有配置 action");
                            RequireAction(s.action, where);
                            break;
                        case ScriptOp.SkillStage:
                            if (string.IsNullOrEmpty(s.action)) problems.Add($"{where} 的 skillStage 没有配置 action");
                            RequireAction(s.action, where);
                            RequireSkill(s.skill, where);
                            RequireIcon(s.icon, where);
                            if (s.windowTicks <= 0) problems.Add($"{where} 的 skillStage window 必须大于 0");
                            break;
                        case ScriptOp.SkillEnd:
                            RequireSkill(s.skill, where);
                            break;
                        case ScriptOp.AddBuff:
                        case ScriptOp.RemoveBuff:
                            RequireBuff(s.buff, where);
                            if (s.toTarget && s.on != ScriptTrigger.Hit) problems.Add($"{where} 的 toTarget 只能用于 hit 触发");
                            break;
                    }
                    if (s.cond == null) continue;
                    foreach (ScriptCondition c in s.cond)
                    {
                        if (!string.IsNullOrEmpty(c.buff)) RequireBuff(c.buff, $"{where} 的条件");
                    }
                }
            }

            foreach (ActionInfo a in template.Actions)
            {
                if (!animKeys.Contains(a.animKey)) problems.Add($"动作 {a.id} 的动画 {a.animKey} 不存在");
                RequireAction(a.autoNextActionId, $"动作 {a.id} 的 autoNextActionId");
                ValidateCommands(a, problems);
                ValidateScripts(a);
                if (a.attacks != null)
                {
                    foreach (AttackInfo atk in a.attacks)
                    {
                        if (atk.selfActionChange.changeType == ActionChangeType.ChangeToActionId) RequireAction(atk.selfActionChange.param, $"动作 {a.id} 的 selfActionChange");
                        RequireAction(atk.hurtAction, $"动作 {a.id} 的 hurtAction");
                        if (atk.grab.enabled)
                        {
                            RequireAction(atk.grab.pose.action, $"动作 {a.id} 的抓取姿势");
                            if (atk.grab.releaseActionChange.changeType == ActionChangeType.ChangeToActionId)
                                RequireAction(atk.grab.releaseActionChange.param, $"动作 {a.id} 的抓取释放动作");
                        }
                    }
                }
                if (a.events == null) continue;
                foreach (ActionEventInfo evt in a.events)
                {
                    if (evt.type == ActionEventType.Spawn && !template.Spawns.ContainsKey(evt.spawnId))
                        problems.Add($"动作 {a.id} 第 {evt.frame} 帧生成了未配置的生成物 {evt.spawnId}");
                    if (evt.type == ActionEventType.Effect) RequireEffect(evt.asset, $"动作 {a.id} 第 {evt.frame} 帧");
                    if (evt.type == ActionEventType.Sound) RequireSound(evt.asset, $"动作 {a.id} 第 {evt.frame} 帧");
                    if (evt.type == ActionEventType.Cinematic) RequireCinematic(evt.asset, $"动作 {a.id} 第 {evt.frame} 帧");
                    if (evt.type == ActionEventType.Buff || evt.type == ActionEventType.BuffRemoved)
                        problems.Add($"动作 {a.id} 第 {evt.frame} 帧的动作事件使用了 {evt.type} 类型，给角色加 Buff 改用 scripts 的 addBuff");
                }
            }

            foreach (SkillInfo skill in template.Skills)
            {
                RequireIcon(skill.icon, $"技能 {skill.id}");
                if (string.IsNullOrEmpty(skill.actionId)) problems.Add($"技能 {skill.id} 没有配置入口动作 actionId");
                RequireAction(skill.actionId, $"技能 {skill.id}");
                if (!string.IsNullOrEmpty(skill.buff)) RequireBuff(skill.buff, $"技能 {skill.id} 的 buff");
            }

            // 换段目标动作：不能是技能入口动作，不能被两个技能共用
            var stageOwner = new Dictionary<string, string>();
            foreach (ActionInfo a in template.Actions)
            {
                if (a.scripts == null) continue;
                foreach (ActionScript s in a.scripts)
                {
                    if (s.op != ScriptOp.SkillStage || string.IsNullOrEmpty(s.action)) continue;
                    if (skillEntries.Contains(s.action)) problems.Add($"动作 {a.id} 的 skillStage 目标 {s.action} 同时是技能入口动作");
                    if (stageOwner.TryGetValue(s.action, out string owner))
                    {
                        if (owner != s.skill) problems.Add($"动作 {s.action} 同时是技能 {owner} 和 {s.skill} 的下一段动作");
                    }
                    else stageOwner.Add(s.action, s.skill);
                }
            }

            foreach (BuffInfo buff in template.Buffs.Values)
            {
                RequireEffect(buff.effect, $"Buff {buff.id}");
                if (buff.durationTicks <= 0) problems.Add($"Buff {buff.id} 的 duration 必须大于 0");
                if (SecretScrollRegistry.HasBuff(buff.id)) problems.Add($"Buff {buff.id} 与秘卷表中的 Buff 重名");
            }

            foreach (SpawnInfo spawn in template.Spawns.Values)
            {
                RequireAction(spawn.initialActionId, $"生成物 {spawn.id} 的初始动作");
                RequireAction(spawn.despawnActionId, $"生成物 {spawn.id} 的消失动作");
            }
            return problems;
        }

        /// <summary>
        /// 触发指令：每条至少一个按键，按键名全部可识别 (无法识别或摇杆键写在 press / release 中时 ToRuntime 记为 None)
        /// </summary>
        public static void ValidateCommands(ActionInfo a, List<string> problems)
        {
            if (a.commands == null) return;
            for (int i = 0; i < a.commands.Length; i++)
            {
                ActionCommand c = a.commands[i];
                int count = c.press.Length + c.hold.Length + c.release.Length;
                if (count == 0) problems.Add($"动作 {a.id} 的第 {i} 条指令没有配置任何按键");
                if (Array.IndexOf(c.press, CommandKey.None) >= 0 || Array.IndexOf(c.hold, CommandKey.None) >= 0 || Array.IndexOf(c.release, CommandKey.None) >= 0)
                    problems.Add($"动作 {a.id} 的第 {i} 条指令含无法识别的按键 (摇杆键只能写在 hold 中)");
            }
        }
    }
}
