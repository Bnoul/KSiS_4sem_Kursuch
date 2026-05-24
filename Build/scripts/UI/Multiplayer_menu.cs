using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;

public class Multiplayer_menu : MonoBehaviour
{
    public GameObject panel;
    public GameObject Main_menu;
    public GameObject serverEntryPrefab;
    public Transform content;
    public TMP_InputField client_name;
    public TextMeshProUGUI errorText;
    public string masterServerUrl = "http://26.175.171.220:6004";

    public static Multiplayer_menu Instance;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private HttpClient http = new HttpClient();

    public void Open()
    {
        panel.SetActive(true);
        RefreshList();
    }

    public void Close()
    {
        Main_menu.SetActive(true);
        panel.SetActive(false);
    }

    public async void RefreshList()
    {
        errorText.text = "";

        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (string.IsNullOrWhiteSpace(masterServerUrl))
        {
            errorText.text = "Ошибка: URL мастер-сервера не задан";
            return;
        }

        try
        {
            http.Timeout = System.TimeSpan.FromSeconds(3);

            string json = await http.GetStringAsync(masterServerUrl + "/servers");

            if (string.IsNullOrWhiteSpace(json))
            {
                errorText.text = "Ошибка: мастер-сервер вернул пустой ответ";
                return;
            }

            List<ServerInfo> servers;

            try
            {
                servers = JsonConvert.DeserializeObject<List<ServerInfo>>(json);
            }
            catch
            {
                errorText.text = "Ошибка: неверный формат данных от сервера";
                return;
            }

            if (servers == null || servers.Count == 0)
            {
                errorText.text = "Нет доступных серверов";
                return;
            }

            foreach (var s in servers)
            {
                var entry = Instantiate(serverEntryPrefab, content);
                entry.GetComponent<Server_entry_UI>().Setup(s);
            }
        }
        catch (HttpRequestException)
        {
            errorText.text = "Ошибка: мастер-сервер недоступен";
        }
        catch (TaskCanceledException)
        {
            errorText.text = "Ошибка: время ожидания истекло";
        }
        catch (System.Exception ex)
        {
            errorText.text = "Неизвестная ошибка: " + ex.Message;
        }
    }
}

    [System.Serializable]
public class ServerInfo
{
    public string id;
    public string name;
    public string ip;
    public int port;
    public string track;
    public int laps;
    public int maxPlayers;
    public int currentPlayers;
}
