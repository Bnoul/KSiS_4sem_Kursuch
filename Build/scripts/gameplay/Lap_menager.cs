using Newtonsoft.Json;
using TMPro;
using UnityEngine;

public class Lap_menager : MonoBehaviour
{
    public static Lap_menager Instance;
    public int lapsToWin = 3;
    public int currentLap = 0;
    public TMP_Text lapText;

    public void laps_change(int laps)
    {
        lapsToWin = laps;
        lapText.text = $"Lap: {currentLap}/{lapsToWin}";
    }

    void Start()
    {
        Instance = this;
        lapText.text = $"Lap: {currentLap}/{lapsToWin}";
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        // проверяем, что это локальная машина
        var car = col.GetComponent<NetworkCar>();
        if (car == null) return;
        if (!car.isLocal) return;

        currentLap++;
        lapText.text = $"Lap: {currentLap}/{lapsToWin}";

        if (currentLap == lapsToWin)
        {
            SendWinPacket();
        }
    }

    void SendWinPacket()
    {
        var packet = new
        {
            type = "win",
            id = TCP_client_connector.Instance.ClientId
        };

        TCP_client_connector.Instance.SendUDP(JsonConvert.SerializeObject(packet));
    }

}
