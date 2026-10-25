using System.Collections.Generic;
using UnityEngine;

namespace KiHan.Asset
{
    /// <summary>
    /// 全局增强分级对象池管理器
    /// 支持类型隔离 (FX, Entity, UI)、开局预热 (Prewarm)、全自动归还追踪与 0 GC 复用
    /// </summary>
    public class ObjectPoolManager : UnitySingleton<ObjectPoolManager>
    {
        private class PoolBucket
        {
            public string Address;
            public PoolCategory Category;
            public Stack<GameObject> Available = new();
            public Transform Root;

            public PoolBucket(string address, PoolCategory category, Transform parentRoot)
            {
                Address = address;
                Category = category;
                GameObject bucketGo = new GameObject($"Bucket_{category}_{address.Replace('/', '_')}");
                bucketGo.transform.SetParent(parentRoot);
                Root = bucketGo.transform;
            }
        }

        private readonly Dictionary<string, PoolBucket> _buckets = new();

        /// <summary>
        /// 直接登记的预制体 (角色包内资源，不走 AssetManager 按地址加载)
        /// </summary>
        private readonly Dictionary<string, GameObject> _registeredPrefabs = new();
        private readonly Dictionary<PoolCategory, Transform> _categoryRoots = new();
        private Transform _poolGlobalRoot;

        protected override void Awake()
        {
            base.Awake();
            InitRoots();
        }

        private void InitRoots()
        {
            if (_poolGlobalRoot == null)
            {
                GameObject rootGo = new GameObject("[ObjectPoolRoot]");
                _poolGlobalRoot = rootGo.transform;
                if (Application.isPlaying) DontDestroyOnLoad(rootGo);

                foreach (PoolCategory cat in System.Enum.GetValues(typeof(PoolCategory)))
                {
                    GameObject catGo = new GameObject(cat.ToString());
                    catGo.transform.SetParent(_poolGlobalRoot);
                    _categoryRoots[cat] = catGo.transform;
                }
            }
        }

        private PoolBucket GetOrCreateBucket(string address, PoolCategory category)
        {
            if (_buckets.TryGetValue(address, out var bucket))
            {
                return bucket;
            }

            InitRoots();
            Transform catRoot = _categoryRoots.TryGetValue(category, out var r) ? r : _poolGlobalRoot;
            bucket = new PoolBucket(address, category, catRoot);
            _buckets[address] = bucket;
            return bucket;
        }

        /// <summary>
        /// 登记预制体：之后该地址的预热与生成直接使用此预制体 (角色包内资源通过清单引用，不在 Resources 中)
        /// </summary>
        public void RegisterPrefab(string address, GameObject prefab)
        {
            if (string.IsNullOrEmpty(address) || prefab == null) return;
            _registeredPrefabs[address] = prefab;
        }

        private GameObject LoadPrefab(string address)
        {
            return _registeredPrefabs.TryGetValue(address, out GameObject prefab) ? prefab : AssetManager.Instance.Load<GameObject>(address);
        }

        /// <summary>
        /// 预热对象池：在加载阶段批量实例化灌入池中，消除战斗运行时卡顿
        /// </summary>
        /// <param name="onCreated">每个新实例创建后调用 (如补挂运行时组件)，在加载阶段执行</param>
        public void Prewarm(string address, int count, PoolCategory category = PoolCategory.Default, System.Action<GameObject> onCreated = null)
        {
            if (string.IsNullOrEmpty(address) || count <= 0) return;

            GameObject prefab = LoadPrefab(address);
            if (prefab == null)
            {
                Debug.LogWarning($"[ObjectPoolManager] 预热失败，未找到预制体: {address}");
                return;
            }

            PoolBucket bucket = GetOrCreateBucket(address, category);
            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(prefab, bucket.Root);
                instance.SetActive(false);

                PoolItemTracker tracker = GetTracker(instance, address, category);
                tracker.IsPooled = true;
                onCreated?.Invoke(instance);

                bucket.Available.Push(instance);
            }
        }

        /// <summary>
        /// 从池中提取实例 (若池空则从 AssetManager 实例化新对象)
        /// </summary>
        public GameObject Spawn(string address, Vector3 position, Quaternion rotation, Transform parent = null, PoolCategory category = PoolCategory.Default)
        {
            if (string.IsNullOrEmpty(address)) return null;

            PoolBucket bucket = GetOrCreateBucket(address, category);
            GameObject instance = null;

            while (bucket.Available.Count > 0)
            {
                instance = bucket.Available.Pop();
                if (instance != null) break;
            }

            if (instance == null)
            {
                GameObject prefab = LoadPrefab(address);
                if (prefab == null)
                {
                    Debug.LogError($"[ObjectPoolManager] 实例化失败，无法加载: {address}");
                    return null;
                }
                instance = Instantiate(prefab, parent != null ? parent : bucket.Root);
            }
            else
            {
                if (parent != null) instance.transform.SetParent(parent);
            }

            instance.transform.SetPositionAndRotation(position, rotation);

            PoolItemTracker tracker = GetTracker(instance, address, category);
            tracker.IsPooled = false;

            instance.SetActive(true);

            // 触发 IPoolable 唤醒生命周期
            IPoolable[] poolables = tracker.Poolables;
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnSpawn();
            }

            return instance;
        }

        private static PoolItemTracker GetTracker(GameObject instance, string address, PoolCategory category)
        {
            if (!instance.TryGetComponent(out PoolItemTracker tracker)) tracker = instance.AddComponent<PoolItemTracker>();
            tracker.PoolAddress = address;
            tracker.Category = category;
            return tracker;
        }

        /// <summary>
        /// 泛型重载：获取并返回挂载的指定组件
        /// </summary>
        public T Spawn<T>(string address, Vector3 position, Quaternion rotation, Transform parent = null, PoolCategory category = PoolCategory.Default) where T : Component
        {
            GameObject go = Spawn(address, position, rotation, parent, category);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// 归还对象到池中 (通过 PoolItemTracker 自动识别归属池)
        /// </summary>
        public void Recycle(GameObject instance)
        {
            if (instance == null) return;

            if (!instance.TryGetComponent(out PoolItemTracker tracker) || string.IsNullOrEmpty(tracker.PoolAddress))
            {
                // 未登记的对象直接销毁
                DestroyPooled(instance);
                return;
            }

            if (tracker.IsPooled)
            {
                // 防止重复归还
                return;
            }

            // 触发 IPoolable 回收生命周期
            IPoolable[] poolables = tracker.Poolables;
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnRecycle();
            }

            tracker.IsPooled = true;
            PoolBucket bucket = GetOrCreateBucket(tracker.PoolAddress, tracker.Category);

            instance.SetActive(false);
            instance.transform.SetParent(bucket.Root);
            bucket.Available.Push(instance);
        }

        /// <summary>
        /// 清理指定分类的全部对象池 (如对局退出时调用 ClearCategory(PoolCategory.FX))
        /// </summary>
        public void ClearCategory(PoolCategory category)
        {
            List<string> keysToRemove = new();
            foreach (var kv in _buckets)
            {
                if (kv.Value.Category == category)
                {
                    while (kv.Value.Available.Count > 0)
                    {
                        var go = kv.Value.Available.Pop();
                        if (go != null) DestroyPooled(go);
                    }
                    if (kv.Value.Root != null) DestroyPooled(kv.Value.Root.gameObject);
                    keysToRemove.Add(kv.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _buckets.Remove(keysToRemove[i]);
            }
        }

        /// <summary>
        /// 清理全局全部对象池与已缓存实例
        /// </summary>
        public void ClearAll()
        {
            foreach (var kv in _buckets)
            {
                while (kv.Value.Available.Count > 0)
                {
                    var go = kv.Value.Available.Pop();
                    if (go != null) DestroyPooled(go);
                }
                if (kv.Value.Root != null) DestroyPooled(kv.Value.Root.gameObject);
            }
            _buckets.Clear();
        }

        /// <summary>
        /// 编辑器非运行状态 (如 Unity 环境测试) 下 Destroy 不可用，改为立即销毁
        /// </summary>
        private static void DestroyPooled(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
