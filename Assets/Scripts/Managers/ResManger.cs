using System.Collections.Generic;
using UnityEngine;
using KiHan.Asset;

namespace Managers
{
    /// <summary>
    /// 资源管理器兼容外观层 (Backward Compatibility Facade)
    /// 内部已完全无缝对接全新的 AssetManager 与 ObjectPoolManager
    /// 保证旧有逻辑代码无损运行，同时直接享受分级对象池与标准化资源管线加速
    /// </summary>
    public class ResManager : UnitySingleton<ResManager>
    {
        public T Load<T>(string path) where T : Object
        {
            return AssetManager.Instance.Load<T>(path);
        }

        public ResourceRequest LoadAsync<T>(string path) where T : Object
        {
            if (path.EndsWith(".prefab")) path = path.Replace(".prefab", "");
            return Resources.LoadAsync<T>(path);
        }

        public void AddToCache(string path, Object obj)
        {
            // 新版架构由 AssetManager 统一根据句柄管理，此处保留接口兼容
        }

        public GameObject Spawn(string path, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return ObjectPoolManager.Instance.Spawn(path, position, rotation, parent);
        }

        public void Recycle(GameObject obj)
        {
            ObjectPoolManager.Instance.Recycle(obj);
        }

        public void Clear()
        {
            ObjectPoolManager.Instance.ClearAll();
            AssetManager.Instance.Clear();
        }
    }
}
