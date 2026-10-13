using System.Collections.Generic;
using KiHan.Asset;
using KiHan.Math;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 战斗角色装配：按角色 ID 加载动画切片与动作配置，组装 CharacterObj + ActionController + FrameAnimator + InputToCommand
    ///
    /// 角色资源目录约定 (Resources 相对路径)：
    /// - {dir}/*.asset        AnimationFrameData，资源名即 animKey
    /// - {dir}/actions.json   ActionDataContainer 动作配置 (String-First)
    /// </summary>
    public static class CharacterFactory
    {
        public const string DefaultActionId = "idle";

        private static readonly Dictionary<int, string> ResourceDirs = new Dictionary<int, string>
        {
            { 90001, "Characters/naruto" },
        };

        public static bool IsKnown(int characterId) => ResourceDirs.ContainsKey(characterId);

        /// <param name="characterId">角色 ID (如 90001 鸣人)</param>
        /// <param name="entityId">逻辑实体 ID (双端一致，决定结算顺序)</param>
        /// <param name="teamId">阵营</param>
        /// <param name="inputSlot">RoomFrame.InputFrames 的玩家槽位</param>
        public static CharacterObj Create(int characterId, int entityId, int teamId, int inputSlot, FPVector3 position, bool facingLeft)
        {
            if (!ResourceDirs.TryGetValue(characterId, out string dir))
            {
                Debug.LogError($"[CharacterFactory] 未知角色 ID: {characterId}");
                return null;
            }

            // 先失活：组件全部就位、引用设置完成后再激活，Awake 才能拿到完整的组件引用
            var go = new GameObject($"Character_{characterId}_E{entityId}");
            go.SetActive(false);

            var anim = go.AddComponent<FrameAnimator>();
            foreach (AnimationFrameData clip in AssetManager.Instance.LoadAll<AnimationFrameData>(dir))
            {
                anim.RegisterAnim(clip.name, clip);
            }

            var action = go.AddComponent<ActionController>();
            action.anim = anim;

            var character = go.AddComponent<CharacterObj>();
            character.EntityId = entityId;
            character.TeamId = teamId;
            character.InputSlot = inputSlot;
            character.action = action;
            character.input = go.AddComponent<InputToCommand>();

            go.SetActive(true);

            action.SetAllActions(LoadActions($"{dir}/actions"), DefaultActionId);
            character.FacingLeft = facingLeft;
            character.motion.Reset(position);
            return character;
        }

        private static List<ActionInfo> LoadActions(string address)
        {
            var actions = new List<ActionInfo>();
            TextAsset json = AssetManager.Instance.Load<TextAsset>(address);
            if (json == null)
            {
                Debug.LogError($"[CharacterFactory] 找不到动作配置: {address}");
                return actions;
            }

            ActionDataContainer container = JsonUtility.FromJson<ActionDataContainer>(json.text);
            if (container?.data == null) return actions;

            foreach (ActionData data in container.data)
            {
                actions.Add(data.ToRuntime());
            }
            return actions;
        }
    }
}
