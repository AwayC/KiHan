using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class ResManager : UnitySingleton<ResManager>
    {
        // 资源缓存 <path, obj>
        private Dictionary<string, Object> _assetCache = new Dictionary<string, Object>();

        // 对象池
        private Dictionary<string, Stack<GameObject>> _pool = new Dictionary<string, Stack<GameObject>>();

        /*
         * 加载资源
         */
        public T Load<T>(string path) where T : Object
        {
            if (_assetCache.TryGetValue(path, out var asset))
            {
                return asset as T;
            }

            T newAsset = Resources.Load<T>(path);
            if (newAsset != null)
            {
                _assetCache[path] = newAsset;
            }
            return newAsset;
        }

        /*
         * 对象池加载
         */
        public GameObject Spawn(string path, Transform parent = null)
        {
            if (_pool.TryGetValue(path, out var stack) && stack.Count > 0)
            {
                GameObject obj = stack.Pop();
                obj.SetActive(true);
                return obj;
            }

            GameObject prefab = Load<GameObject>(path);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, parent);
                instance.name = path;
                return instance;
            }

            Debug.LogError($"[Res] 资源加载失败: {path}");
            return null;
        }

        /*
         * 放入对象池
         */
        public void Recycle(GameObject obj)
        {
            string path = obj.name;
            obj.SetActive(false);

            if (!_pool.ContainsKey(path))
            {
                _pool[path] = new Stack<GameObject>();
            }
            _pool[path].Push(obj);
        }

        /*
         * 清空
         */
        public void Clear()
        {
            _assetCache.Clear();
            _pool.Clear();
            Resources.UnloadUnusedAssets();
        }
    }
}