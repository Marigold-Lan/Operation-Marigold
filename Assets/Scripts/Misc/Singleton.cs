using UnityEngine;

/// <summary>
/// MonoBehaviour 单例基类。子类继承后可通过 T.Instance 访问，无需 FindObjectOfType。
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

#if UNITY_2023_1_OR_NEWER
            _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
            _instance = FindObjectOfType<T>();
#endif
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
