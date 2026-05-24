using UnityEngine;
using UnityEngine.SceneManagement;

public class Exit_to_menu : MonoBehaviour
{
    public void Exit()
    {
        TCP_client_connector.Instance.Disconnect();
        SceneManager.LoadScene("Main_menu");
    }
}
