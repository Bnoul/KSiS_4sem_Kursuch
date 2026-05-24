using UnityEngine;

public class ServerHolder : MonoBehaviour
{
    public static ServerHolder Instance;
    public Server_log Server;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }
}
