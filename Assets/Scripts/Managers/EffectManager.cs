using UnityEngine;
using System.Collections.Generic;
using KiHan.Logic;
using View;

namespace Managers
{
    /// <summary>
    /// 表现层特效管理器 (对象池实现)
    /// 监听逻辑层事件，负责加载、实例化和管理 EffectNode
    /// </summary>
    public class EffectManager : UnitySingleton<EffectManager>
    {
        private Dictionary<string, string> _aliasMap = new Dictionary<string, string>();
        private Dictionary<string, Queue<EffectNode>> _pool = new Dictionary<string, Queue<EffectNode>>();
        private Dictionary<int, EffectNode> _activeEffects = new Dictionary<int, EffectNode>();
        private int _nextEffectId = 1;

        protected override void Awake()
        {
            base.Awake();
            EventManager.Instance.AddListener("PlayEffect", OnPlayEffectEvent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (EventManager.Instance != null)
            {
                EventManager.Instance.RemoveListener("PlayEffect", OnPlayEffectEvent);
            }
        }

        private string GetResourcePath(string effectName)
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
        /// 预加载特效到对象池，支持别名注册
        /// </summary>
        public void Preload(string resourcePath, int count, string alias = null)
        {
            if (string.IsNullOrEmpty(resourcePath)) return;

            string key = string.IsNullOrEmpty(alias) ? resourcePath : alias;

            // 注册别名
            if (!string.IsNullOrEmpty(alias))
            {
                _aliasMap[alias] = resourcePath;
            }

            if (!_pool.ContainsKey(key))
            {
                _pool[key] = new Queue<EffectNode>();
            }

            string resPath = GetResourcePath(key);
            GameObject prefab = Resources.Load<GameObject>(resPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[EffectManager] 预加载失败，找不到特效: {resPath}");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                GameObject go = Instantiate(prefab, transform);
                go.SetActive(false);
                var node = go.GetComponent<EffectNode>() ?? go.AddComponent<EffectNode>();
                node.PoolKey = key; // 记录池子归属
                _pool[key].Enqueue(node);
            }
        }

        private void OnPlayEffectEvent(object dataObj)
        {
            if (dataObj is EffectData data)
            {
                PlayEffect(data);
            }
        }

        /// <summary>
        /// 播放特效，返回句柄ID
        /// </summary>
        public int PlayEffect(EffectData data)
        {
            if (string.IsNullOrEmpty(data.EffectName)) return -1;

            EffectNode node = GetFromPool(data.EffectName);
            if (node == null) return -1;

            int id = _nextEffectId++;
            _activeEffects[id] = node;

            node.gameObject.SetActive(true);
            node.Init(id, data);

            return id;
        }

        /// <summary>
        /// 提前终止某个特效
        /// </summary>
        public void StopEffect(int effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out EffectNode node))
            {
                node.Recycle();
            }
        }

        private EffectNode GetFromPool(string effectName)
        {
            if (_pool.ContainsKey(effectName) && _pool[effectName].Count > 0)
            {
                return _pool[effectName].Dequeue();
            }

            // 动态加载作为后备手段
            string resPath = GetResourcePath(effectName);
            GameObject prefab = Resources.Load<GameObject>(resPath);
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab, transform);
                var node = go.GetComponent<EffectNode>() ?? go.AddComponent<EffectNode>();
                node.PoolKey = effectName; // 记录池子归属
                return node;
            }

            Debug.LogWarning($"[EffectManager] 播放失败，找不到特效: {resPath}");
            return null;
        }

        public void RecycleEffect(EffectNode node)
        {
            if (node == null) return;
            
            node.gameObject.SetActive(false);
            node.transform.SetParent(this.transform);

            // 清理活跃列表
            if (_activeEffects.ContainsKey(node.EffectId))
            {
                _activeEffects.Remove(node.EffectId);
            }

            // 放回对应的池子，实现真正的对象池回收
            if (!string.IsNullOrEmpty(node.PoolKey))
            {
                if (!_pool.ContainsKey(node.PoolKey))
                {
                    _pool[node.PoolKey] = new Queue<EffectNode>();
                }
                _pool[node.PoolKey].Enqueue(node);
            }
            else
            {
                // 没有 PoolKey (异常情况)，安全销毁
                Destroy(node.gameObject); 
            }
        }
    }
}
