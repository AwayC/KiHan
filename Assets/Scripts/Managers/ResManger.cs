using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class ResManager : UnitySingleton<ResManager>
    {
        private Dictionary<string, Object> _assetCache = new();
        private Dictionary<string, Stack<GameObject>> _pool = new();
        private Transform _poolRoot;

        protected override void Awake()
        {
            base.Awake();
            _poolRoot = new GameObject("PoolRoot").transform;
            DontDestroyOnLoad(_poolRoot);
        }

        public T Load<T>(string path) where T : Object
        {
            if (path.EndsWith(".prefab")) path = path.Replace(".prefab", "");

            if (_assetCache.TryGetValue(path, out var asset))
            {
                return asset as T;
            }

            T newAsset = Resources.Load<T>(path);
            if (newAsset != null)
            {
                _assetCache[path] = newAsset;
            }
            else
            {
                Debug.LogError($"[Res] 资源加载失败: {path}");
            }
            return newAsset;
        }

        public GameObject Spawn(string path, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            GameObject obj = null;
            if (_pool.TryGetValue(path, out var stack) && stack.Count > 0)
            {
                obj = stack.Pop();
                obj.SetActive(true);
                obj.transform.SetParent(parent);
            }
            else
            {
                GameObject prefab = Load<GameObject>(path);
                if (prefab != null)
                {
                    obj = Instantiate(prefab, parent);
                    obj.name = path;
                }
            }

            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            return obj;
        }

        public void Recycle(GameObject obj)
        {
            if (obj == null) return;

            string path = obj.name;
            obj.SetActive(false);
            obj.transform.SetParent(_poolRoot);

            if (!_pool.ContainsKey(path)) _pool[path] = new Stack<GameObject>();
            _pool[path].Push(obj);
        }

        public void Clear()
        {
            _assetCache.Clear();
            foreach (var stack in _pool.Values)
            {
                while (stack.Count > 0) Destroy(stack.Pop());
            }
            _pool.Clear();
            Resources.UnloadUnusedAssets();
        }
    }
}