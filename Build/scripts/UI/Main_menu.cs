using UnityEngine;

public class Main_menu : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject multiplayerPanel;
    public GameObject hostingPanel;
    public GameObject singleplayerPanel;

    public void OpenMultiplayer()
    {
        mainMenuPanel.SetActive(false);
        multiplayerPanel.SetActive(true);
        Multiplayer_menu.Instance.Open();
    }

    public void OpenHosting()
    {
        mainMenuPanel.SetActive(false);
        hostingPanel.SetActive(true);
    }

    public void OpenSingleplayer()
    {
        mainMenuPanel.SetActive(false);
        singleplayerPanel.SetActive(true);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
