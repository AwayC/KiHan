using UnityEngine;

// 普通单例 - 用于纯逻辑层（Logic）
public abstract class Singleton<T> where T : new()
{
    private static T _instance;
    private static readonly object mutex = new object();

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (mutex)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }
}

// Unity 单例 - 用于表现层（View/Manager）
public class UnitySingleton<T> : MonoBehaviour where T : Component
{
    private static T _instance = null;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject();
                    _instance = obj.AddComponent<T>();
                    // 保证在场景切换时不被销毁
                    DontDestroyOnLoad(obj);
                    obj.name = typeof(T).Name;
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 如果场景中已经手动挂载了一个单例，确保不会重复
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] 发现重复的单例 {typeof(T).Name}，已自动删除。");
            Destroy(this.gameObject);
        }
    }
}