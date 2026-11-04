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

            foreach (ActionInfo a in template.Actions)
            {
                if (!animKeys.Contains(a.animKey)) problems.Add($"动作 {a.id} 的动画 {a.animKey} 不存在");
                RequireAction(a.autoNextActionId, $"动作 {a.id} 的 autoNextActionId");
                ValidateCommands(a, problems);
                if (a.hitBranches != null)
                {
                    foreach (HitBranch branch in a.hitBranches) RequireAction(branch.actionId, $"动作 {a.id} 第 {branch.frame} 帧的命中分支");
                }
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
                    if (evt.type == ActionEventType.Buff && !template.Buffs.ContainsKey(evt.asset))
                        problems.Add($"动作 {a.id} 第 {evt.frame} 帧添加了未配置的 Buff {evt.asset}");
                }
            }

            foreach (SkillInfo skill in template.Skills)
            {
                if (skill.stages.Length == 0) problems.Add($"技能 {skill.id} 没有配置段");
                RequireIcon(skill.icon, $"技能 {skill.id}");
                foreach (SkillStageInfo stage in skill.stages)
                {
                    RequireAction(stage.actionId, $"技能 {skill.id}");
                    foreach (string follow in stage.followActions) RequireAction(follow, $"技能 {skill.id} 的后续动作");
                    RequireIcon(stage.icon, $"技能 {skill.id} 的段 {stage.actionId}");
                }
            }

            foreach (BuffInfo buff in template.Buffs.Values) RequireEffect(buff.effect, $"Buff {buff.id}");

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
