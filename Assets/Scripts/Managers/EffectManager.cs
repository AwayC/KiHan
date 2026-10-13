using UnityEngine;
using System.Collections.Generic;
using KiHan.Asset;
using KiHan.View.Render;

namespace Managers
{
    /// <summary>
    /// 表现层特效管理器
    /// 核心能力：
    /// 1. 对接 3D 纵深特效组件 (EffectView)，未挂载的特效预制体自动补挂
    /// 2. 底层接入分级对象池 (全池化与开局预热，0 GC)
    /// </summary>
    public class EffectManager : UnitySingleton<EffectManager>
    {
        private readonly Dictionary<string, string> _aliasMap = new();
        private readonly Dictionary<int, GameObject> _activeEffectObjects = new();
        private int _nextEffectId = 1;

        public string GetResourcePath(string effectName)
        {
            if (_aliasMap.TryGetValue(effectName, out string mappedPath))
            {
                return mappedPath;
            }
            if (effectName.Contains("/"))
            {
                return effectName;
            }
            return $"Effects/{effectName}";
        }

        /// <summary>
        /// 预加载特效到全局分级对象池
        /// </summary>
        public void Preload(string resourcePath, int count, string alias = null)
        {
            if (string.IsNullOrEmpty(resourcePath)) return;

            string key = string.IsNullOrEmpty(alias) ? resourcePath : alias;
            if (!string.IsNullOrEmpty(alias))
            {
                _aliasMap[alias] = resourcePath;
            }

            string resPath = GetResourcePath(key);
            ObjectPoolManager.Instance.Prewarm(resPath, count, PoolCategory.FX);
        }

        /// <summary>
        /// 播放特效，返回句柄 ID
        /// </summary>
        public int PlayEffect(EffectData data)
        {
            if (string.IsNullOrEmpty(data.EffectName)) return -1;

            string resPath = GetResourcePath(data.EffectName);
            int id = _nextEffectId++;

            // 1. 从增强分级对象池获取实例
            GameObject effectGo = ObjectPoolManager.Instance.Spawn(
                resPath, 
                Vector3.zero, 
                Quaternion.identity, 
                null, 
                PoolCategory.FX
            );

            if (effectGo == null)
            {
                Debug.LogWarning($"[EffectManager] 特效生成失败，找不到资源: {resPath}");
                return -1;
            }

            _activeEffectObjects[id] = effectGo;

            var effectView = effectGo.GetComponent<EffectView>();
            if (effectView == null) effectView = effectGo.AddComponent<EffectView>();

            effectView.PoolKey = data.EffectName;
            effectView.Init(id, data);

            return id;
        }

        /// <summary>
        /// 提前手动终止某个特效
        /// </summary>
        public void StopEffect(int effectId)
        {
            if (_activeEffectObjects.TryGetValue(effectId, out GameObject effectGo))
            {
                _activeEffectObjects.Remove(effectId);
                if (effectGo != null)
                {
                    var effectView = effectGo.GetComponent<EffectView>();
                    if (effectView != null)
                    {
                        effectView.RecycleSelf();
                    }
                    else
                    {
                        ObjectPoolManager.Instance.Recycle(effectGo);
                    }
                }
            }
        }

        /// <summary>
        /// 清理局内所有活跃特效
        /// </summary>
        public void Clear()
        {
            _activeEffectObjects.Clear();
            ObjectPoolManager.Instance.ClearCategory(PoolCategory.FX);
        }
    }
}
