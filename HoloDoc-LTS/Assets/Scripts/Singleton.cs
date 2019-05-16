using UnityEngine;

public class MySingleton<T> : MonoBehaviour where T : MySingleton<T>
{
    private static T _Instance;
    public static T Instance
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = FindObjectOfType<T>();
            }
            return _Instance;
        }
    }
}
