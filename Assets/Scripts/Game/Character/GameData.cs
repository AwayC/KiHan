using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Logic
{
    /// <summary>
    /// 全局静态动作与战斗数据加载中心
    /// 负责将 JSON 配置确定性转换为逻辑层运行时结构 (0 跨平台浮点误差)
    /// </summary>
    public static class GameData
    {
        private static bool _loaded = false;
        public static readonly Dictionary<string, ActionInfo> Actions = new Dictionary<string, ActionInfo>();

        public static void Load(string resourcePath = "GameData/Action")
        {
            if (_loaded) return;
            _loaded = true;
            Actions.Clear();

            TextAsset ta = Resources.Load<TextAsset>(resourcePath);
            if (ta != null && !string.IsNullOrEmpty(ta.text))
            {
                ActionDataContainer container = JsonUtility.FromJson<ActionDataContainer>(ta.text);
                if (container != null && container.data != null)
                {
                    foreach (ActionData ad in container.data)
                    {
                        if (!string.IsNullOrEmpty(ad.id))
                        {
                            Actions[ad.id] = ad.ToRuntime();
                        }
                    }
                }
            }
        }

        public static void Reload(string resourcePath = "GameData/Action")
        {
            _loaded = false;
            Load(resourcePath);
        }

        public static List<ActionInfo> AllActions()
        {
            return new List<ActionInfo>(Actions.Values);
        }

        public static ActionInfo GetAction(string id)
        {
            Actions.TryGetValue(id, out ActionInfo info);
            return info;
        }

        public static bool TryGetAction(string id, out ActionInfo info)
        {
            return Actions.TryGetValue(id, out info);
        }
    }
}
