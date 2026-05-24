using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class Server_entry_UI : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI playersText;
    public TextMeshProUGUI trackText;
    public Button connectButton;

    private ServerInfo info;

    public void Setup(ServerInfo server)
    {
        info = server;

        nameText.text = server.name;
        playersText.text = $"{server.currentPlayers}/{server.maxPlayers}";
        trackText.text = server.track;

        connectButton.onClick.AddListener(Connect);
    }

    void Connect()
    {
        if (string.IsNullOrWhiteSpace(info.ip))
        {
            Debug.LogError("Ошибка: IP сервера пустой");
            return;
        }

        if (info.port <= 0)
        {
            Debug.LogError("Ошибка: порт сервера некорректный");
            return;
        }

        Debug.Log($"[CLIENT] Connecting to {info.ip}:{info.port}");
        // Подключаемся через TCP
        bool ok = TCP_client_connector.Instance.Connect(info.ip, info.port);

        if (!ok)
        {
            Debug.LogError("[CLIENT] Не удалось подключиться к серверу");
            return;
        }

        // отправляем имя
        string playerName = Multiplayer_menu.Instance.client_name.text;
        playerName = playerName == "" ? "Player" : playerName;
        TCP_client_connector.Instance.SendName(playerName);

        Debug.Log("[CLIENT] Успешное подключение!");

        SceneManager.LoadScene("Main_scene");
        //SceneManager.UnloadScene("Main_menu");
    }
}
