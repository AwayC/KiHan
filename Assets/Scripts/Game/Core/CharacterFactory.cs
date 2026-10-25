using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Config;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 角色配置模板：一个角色 ID 的动画切片、动作库、技能与生成物配置 (加载一次，玩家与生成物实例共享，只读)
    /// </summary>
    public class CharacterTemplate
    {
        public int Id;

        /// <summary>
        /// 来源角色包 (表现层按其资源表解析特效与图标 key)
        /// </summary>
        public CharacterManifest Manifest;

        public AnimationFrameData[] Anims;
        public List<ActionInfo> Actions;
        public SkillInfo[] Skills;
        public int ChakraMax;
        public int ChakraStart;
        public readonly Dictionary<string, SpawnInfo> Spawns = new Dictionary<string, SpawnInfo>();
        public readonly Dictionary<string, BuffInfo> Buffs = new Dictionary<string, BuffInfo>();
    }

    /// <summary>
    /// 战斗角色装配：按角色 ID 加载模板，组装 CharacterObj + ActionController + FrameAnimator (+ InputToCommand)
    ///
    /// 角色 ID -> 角色包清单 (CharacterManifest) 由角色注册表 (CharacterRegistry) 提供；清单直接引用：
    /// - animations   AnimationFrameData，资源名即 animKey
    /// - actions      ActionDataContainer 动作配置 (String-First)
    /// - skills       SkillDataContainer 技能配置 (可选)
    /// - spawns       SpawnDataContainer 生成物配置 (可选，动作库与本角色共用)
    /// - buffs        BuffDataContainer Buff 配置 (可选，动作事件 Buff 按 id 引用)
    /// </summary>
    public static class CharacterFactory
    {
        public const string DefaultActionId = "idle";

        private static readonly Dictionary<int, CharacterTemplate> Templates = new Dictionary<int, CharacterTemplate>();

        public static bool IsKnown(int characterId) => CharacterRegistry.Find(characterId) != null;

        /// <summary>
        /// 获取 (首次调用时加载) 角色模板，未注册或未启用的角色返回 null
        /// </summary>
        public static CharacterTemplate GetTemplate(int characterId)
        {
            if (Templates.TryGetValue(characterId, out CharacterTemplate template)) return template;
            CharacterManifest manifest = CharacterRegistry.Find(characterId);
            if (manifest == null)
            {
                Debug.LogError($"[CharacterFactory] 角色注册表中没有已启用的角色 {characterId}");
                return null;
            }

            template = BuildTemplate(manifest);
            Templates[characterId] = template;
            return template;
        }

        /// <summary>
        /// 按角色包清单解析出模板 (不缓存；编辑器校验未注册的角色包时也直接调用)
        /// </summary>
        public static CharacterTemplate BuildTemplate(CharacterManifest manifest)
        {
            var template = new CharacterTemplate
            {
                Id = manifest.id,
                Manifest = manifest,
                Anims = manifest.animations.FindAll(clip => clip != null).ToArray(),
                Actions = LoadActions(manifest)
            };

            SkillDataContainer skills = LoadJson<SkillDataContainer>(manifest.skills);
            template.ChakraMax = skills != null ? skills.chakraMax : 0;
            template.ChakraStart = skills != null ? skills.chakraStart : 0;
            int skillCount = skills?.data != null ? skills.data.Length : 0;
            template.Skills = new SkillInfo[skillCount];
            for (int i = 0; i < skillCount; i++) template.Skills[i] = skills.data[i].ToRuntime();

            SpawnDataContainer spawns = LoadJson<SpawnDataContainer>(manifest.spawns);
            if (spawns?.data != null)
            {
                foreach (SpawnData data in spawns.data)
                {
                    SpawnInfo info = data.ToRuntime();
                    template.Spawns[info.id] = info;
                }
            }

            BuffDataContainer buffs = LoadJson<BuffDataContainer>(manifest.buffs);
            if (buffs?.data != null)
            {
                foreach (BuffData data in buffs.data)
                {
                    BuffInfo info = data.ToRuntime();
                    template.Buffs[info.id] = info;
                }
            }

            return template;
        }

        /// <summary>
        /// 退出战斗时释放模板 (下次进入重新读取配置)
        /// </summary>
        public static void ClearTemplates()
        {
            Templates.Clear();
        }

        /// <param name="characterId">角色 ID (如 90001 鸣人)</param>
        /// <param name="entityId">逻辑实体 ID (双端一致，决定结算顺序)</param>
        /// <param name="teamId">阵营</param>
        /// <param name="inputSlot">RoomFrame.InputFrames 的玩家槽位</param>
        public static CharacterObj Create(int characterId, int entityId, int teamId, int inputSlot, FPVector3 position, bool facingLeft)
        {
            CharacterTemplate template = GetTemplate(characterId);
            if (template == null) return null;

            CharacterObj character = Build(template, $"Character_{characterId}_E{entityId}", true);
            character.EntityId = entityId;
            character.TeamId = teamId;
            character.InputSlot = inputSlot;

            var skills = new SkillController();
            skills.Setup(template.Skills, template.ChakraMax, template.ChakraStart);
            character.Skills = skills;

            character.FacingLeft = facingLeft;
            character.motion.Reset(position);
            return character;
        }

        /// <summary>
        /// 装配生成物实例 (由 SpawnPool 在加载阶段预建)：Actor 带输入组件与大脑，Simple 不接收输入
        /// </summary>
        public static CharacterObj CreateSpawn(CharacterTemplate template, SpawnInfo info)
        {
            bool actor = info.type == SpawnType.Actor;
            CharacterObj entity = Build(template, $"Spawn_{template.Id}_{info.id}", actor);
            entity.Spawn = info;
            if (actor) entity.Brain = new CloneBrain();
            return entity;
        }

        private static CharacterObj Build(CharacterTemplate template, string name, bool withInput)
        {
            // 先失活：组件全部就位、引用设置完成后再激活，Awake 才能拿到完整的组件引用
            var go = new GameObject(name);
            go.SetActive(false);

            var anim = go.AddComponent<FrameAnimator>();
            foreach (AnimationFrameData clip in template.Anims)
            {
                anim.RegisterAnim(clip.name, clip);
            }

            var action = go.AddComponent<ActionController>();
            action.anim = anim;

            var character = go.AddComponent<CharacterObj>();
            character.CharacterId = template.Id;
            character.action = action;
            if (withInput) character.input = go.AddComponent<InputToCommand>();

            go.SetActive(true);

            action.SetAllActions(template.Actions, DefaultActionId);
            return character;
        }

        private static List<ActionInfo> LoadActions(CharacterManifest manifest)
        {
            var actions = new List<ActionInfo>();
            ActionDataContainer container = LoadJson<ActionDataContainer>(manifest.actions);
            if (container?.data == null)
            {
                Debug.LogError($"[CharacterFactory] 角色包 {manifest.name} ({manifest.id}) 没有动作配置 (Config/actions.json)");
                return actions;
            }

            foreach (ActionData data in container.data)
            {
                actions.Add(data.ToRuntime());
            }
            return actions;
        }

        private static T LoadJson<T>(TextAsset json) where T : class
        {
            return json != null ? JsonUtility.FromJson<T>(json.text) : null;
        }
    }

    /// <summary>
    /// 生成物实例池 (ISpawnProvider)：加载阶段按 spawns.json 的 prewarm 预建实例，局内只做取出 / 归还
    /// </summary>
    public class SpawnPool : ISpawnProvider
    {
        private readonly Dictionary<int, Dictionary<string, Stack<CharacterObj>>> _free = new Dictionary<int, Dictionary<string, Stack<CharacterObj>>>();
        private readonly List<CharacterObj> _instances = new List<CharacterObj>();

        /// <summary>
        /// 全部已创建的实例 (表现层据此预建视图；退出战斗时统一销毁)
        /// </summary>
        public IReadOnlyList<CharacterObj> Instances => _instances;

        /// <summary>
        /// 预建某个角色全部生成物的实例 (同一角色只预建一次，首次预建返回 true)
        /// </summary>
        public bool Prewarm(int characterId)
        {
            if (_free.ContainsKey(characterId)) return false;
            CharacterTemplate template = CharacterFactory.GetTemplate(characterId);
            if (template == null) return false;

            var byId = new Dictionary<string, Stack<CharacterObj>>();
            _free[characterId] = byId;
            foreach (SpawnInfo info in template.Spawns.Values)
            {
                var stack = new Stack<CharacterObj>(info.prewarm);
                byId[info.id] = stack;
                for (int i = 0; i < info.prewarm; i++)
                {
                    stack.Push(CreateInstance(template, info));
                }
            }
            return true;
        }

        public CharacterObj Acquire(CharacterObj source, string spawnId)
        {
            if (!_free.TryGetValue(source.CharacterId, out var byId) || !byId.TryGetValue(spawnId, out Stack<CharacterObj> stack))
            {
                Debug.LogError($"[SpawnPool] 角色 {source.CharacterId} 没有生成物配置: {spawnId}");
                return null;
            }

            if (stack.Count > 0) return stack.Pop();

            // 预建数量不足：局内补建 (会产生分配，应调大 spawns.json 中的 prewarm)
            CharacterTemplate template = CharacterFactory.GetTemplate(source.CharacterId);
            Debug.LogWarning($"[SpawnPool] 生成物 {spawnId} 预建数量不足，局内补建实例");
            return CreateInstance(template, template.Spawns[spawnId]);
        }

        public void Release(CharacterObj entity)
        {
            if (entity == null || entity.Spawn == null) return;
            if (_free.TryGetValue(entity.CharacterId, out var byId) && byId.TryGetValue(entity.Spawn.id, out Stack<CharacterObj> stack))
            {
                stack.Push(entity);
            }
        }

        /// <summary>
        /// 退出战斗：销毁全部实例
        /// </summary>
        public void Dispose()
        {
            foreach (CharacterObj entity in _instances)
            {
                if (entity != null) Object.Destroy(entity.gameObject);
            }
            _instances.Clear();
            _free.Clear();
        }

        private CharacterObj CreateInstance(CharacterTemplate template, SpawnInfo info)
        {
            CharacterObj entity = CharacterFactory.CreateSpawn(template, info);
            _instances.Add(entity);
            return entity;
        }
    }
}
