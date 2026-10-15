using System;
using UnityEngine;
using System.Collections.Generic;
using KiHan.Asset;
using KiHan.View.Render;

namespace Managers
{
    /// <summary>
    /// 表现层特效管理器
    /// 核心能力：
    /// 1. 对接 3D 纵深特效组件 (EffectView)，未挂载的特效预制体在预热时补挂
    /// 2. 底层接入分级对象池 (开局预热，局内只做池内复用，0 GC)
    /// 3. 句柄管理：PlayEffect 返回句柄，可提前 StopEffect；特效自然播完回收时自动注销句柄
    /// </summary>
    public class EffectManager : UnitySingleton<EffectManager>
    {
        private readonly Dictionary<string, string> _aliasMap = new();
        private readonly Dictionary<int, EffectView> _activeEffects = new();
        private int _nextEffectId = 1;
        private Action<EffectView> _onRecycled;
        private static readonly Action<GameObject> EnsureEffectViewAction = go => EnsureEffectView(go);

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
        /// 预加载特效到全局分级对象池 (加载阶段调用)
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
            ObjectPoolManager.Instance.Prewarm(resPath, count, PoolCategory.FX, EnsureEffectViewAction);
        }

        /// <summary>
        /// 播放特效，返回句柄 ID (失败返回 -1)
        /// </summary>
        /// <param name="loop">循环特效：不自动回收，必须调用 StopEffect 停止</param>
        public int PlayEffect(EffectData data, bool loop = false)
        {
            if (string.IsNullOrEmpty(data.EffectName)) return -1;

            string resPath = GetResourcePath(data.EffectName);

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

            // 未预热的实例 (池不足时新建) 首次使用时补挂组件
            EffectView effectView = EnsureEffectView(effectGo);

            int id = _nextEffectId++;
            _onRecycled ??= OnEffectRecycled;
            effectView.Recycled = _onRecycled;
            effectView.PoolKey = data.EffectName;
            effectView.Init(id, data, loop);
            _activeEffects[id] = effectView;

            return id;
        }

        /// <summary>
        /// 提前手动终止某个特效 (已自然回收的句柄忽略)
        /// </summary>
        public void StopEffect(int effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out EffectView effectView) && effectView != null)
            {
                effectView.RecycleSelf();
            }
            _activeEffects.Remove(effectId);
        }

        /// <summary>
        /// 特效当前是否仍在播放
        /// </summary>
        public bool IsPlaying(int effectId) => _activeEffects.ContainsKey(effectId);

        /// <summary>
        /// 获取仍在播放的特效 (用于跟随锚点逐帧定位)
        /// </summary>
        public bool TryGetEffect(int effectId, out EffectView view) => _activeEffects.TryGetValue(effectId, out view) && view != null;

        /// <summary>
        /// 清理局内所有活跃特效
        /// </summary>
        public void Clear()
        {
            _activeEffects.Clear();
            ObjectPoolManager.Instance.ClearCategory(PoolCategory.FX);
        }

        private void OnEffectRecycled(EffectView view)
        {
            if (_activeEffects.TryGetValue(view.EffectId, out EffectView active) && active == view)
            {
                _activeEffects.Remove(view.EffectId);
            }
        }

        private static EffectView EnsureEffectView(GameObject go)
        {
            if (go.TryGetComponent(out EffectView view)) return view;

            view = go.AddComponent<EffectView>();
            if (go.TryGetComponent(out PoolItemTracker tracker)) tracker.RefreshPoolables();
            return view;
        }
    }
}
