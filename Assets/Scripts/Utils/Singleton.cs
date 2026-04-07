using UnityEngine;

// 普通单例 
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

// Unity 单例
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

                    DontDestroyOnLoad(obj);
                    obj.name = typeof(T).Name;
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[Singleton] Instance already exists, destroying duplicate: {this.gameObject.name}");
            Destroy(this.gameObject);
        }
    }
}