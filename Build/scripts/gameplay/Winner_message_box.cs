using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class WinnerMessageBox : MonoBehaviour
{
    public static WinnerMessageBox Instance;

    public GameObject root;      
    public TMP_Text messageText; 

    void Awake()
    {
        Instance = this;
        root.SetActive(false);
    }

    public void Show(string winnerName)
    {
        Debug.Log("[WinnerMessageBox] Showing winner: " + winnerName);
        messageText.text = $"{winnerName} — победил!";
        root.SetActive(true);

        // Останавливаем игру
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        root.SetActive(false);

        // Возвращаем игру
        Time.timeScale = 1f;
    }

    public void Close()
    {
        TCP_client_connector.Instance.Disconnect();
        SceneManager.LoadScene("Main_menu");
    }
}
