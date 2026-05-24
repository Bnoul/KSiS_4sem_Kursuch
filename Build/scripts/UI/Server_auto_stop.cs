using UnityEngine;

public class ServerAutoStop : MonoBehaviour
{
    void OnApplicationQuit()
    {
        if (ServerHolder.Instance != null && ServerHolder.Instance.Server != null)
        {
            ServerHolder.Instance.Server.Stop();
            ServerHolder.Instance.Server = null;
        }
    }

    /*void OnDestroy()
    {
        if (ServerHolder.Instance != null && ServerHolder.Instance.Server != null)
        {
            ServerHolder.Instance.Server.Stop();
            ServerHolder.Instance.Server = null;
        }
    }*/

}
