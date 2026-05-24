using System.Threading;
using TMPro;
using UnityEngine;

public class Hosting : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public GameObject Main_menu;
    public TMP_InputField nameInput;
    public TMP_InputField portInput;
    public TMP_Dropdown trackDropdown;
    public TMP_InputField lapsInput;
    public TMP_InputField maxPlayersInput;

    [Header("Running UI")]
    public GameObject runningPanel;
    public TextMeshProUGUI statusText;

    [Header("Errors")]
    public TextMeshProUGUI errorText;

    private Thread serverThread;

    void Start()
    {
        runningPanel.SetActive(false);
        errorText.text = "";
        if (ServerHolder.Instance.Server != null)
        {
            runningPanel.SetActive(true);
            statusText.text = $"Сервер запущен на порту {ServerHolder.Instance.Server.port}";
        }
    }

    public void Open()
    {
        panel.SetActive(true);
        runningPanel.SetActive(false);
        errorText.text = "";
    }

    public void Close()
    {
        Main_menu.SetActive(true);
        panel.SetActive(false);
    }

    public void CreateServer()
    {
        errorText.text = "";
        if (ServerHolder.Instance.Server != null)
        {
            errorText.text = "Сервер уже запущен!";
            return;
        }

        // Проверка порта
        if (!int.TryParse(portInput.text, out int port) || port < 1024 || port > 65535)
        {
            errorText.text = "Ошибка: порт должен быть числом от 1024 до 65535";
            return;
        }

        // Проверка кругов
        if (!int.TryParse(lapsInput.text, out int laps) || laps <= 0 || laps > 100)
        {
            errorText.text = "Ошибка: количество кругов должно быть положительным числом от 1 до 100";
            return;
        }

        // Проверка игроков
        if (!int.TryParse(maxPlayersInput.text, out int maxPlayers) || maxPlayers <= 0 || maxPlayers > 8)
        {
            errorText.text = "Ошибка: максимальное число игроков должно быть от 1 до 8";
            return;
        }

        // Проверка трассы
        if (trackDropdown.options.Count == 0)
        {
            errorText.text = "Ошибка: список трасс пуст";
            return;
        }

        string track = trackDropdown.options[trackDropdown.value].text;
        string name = string.IsNullOrWhiteSpace(nameInput.text) ? "Unity Server" : nameInput.text;

        // Создаём серверный объект
        ServerHolder.Instance.Server = new Server_log(port, track, laps, maxPlayers, name);

        // Запускаем сервер в отдельном потоке
        serverThread = new Thread(ServerHolder.Instance.Server.Run);
        serverThread.IsBackground = true;
        serverThread.Start();

        Thread.Sleep(500);

        if (!ServerHolder.Instance.Server.MasterConnected)
        {
            errorText.text = "Ошибка: невозможно подключиться к мастер‑серверу!";
            ServerHolder.Instance.Server.Stop();
            ServerHolder.Instance.Server = null;
            return;
        }
        runningPanel.SetActive(true);
        statusText.text = $"Сервер запущен на порту {port}";
    }

    public void StopServer()
    {
        if (ServerHolder.Instance.Server != null)
        {
            ServerHolder.Instance.Server.Stop();
            ServerHolder.Instance.Server = null;
        }

        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Join(2000);
            serverThread = null;
        }
        runningPanel.SetActive(false);
        statusText.text = "Сервер остановлен";
    }
}
