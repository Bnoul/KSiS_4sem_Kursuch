using UnityEngine;

public class UI_root : MonoBehaviour
{
    public GameObject mainMenuPanel;
    public GameObject multiplayerPanel;
    public GameObject hostingPanel;
    public GameObject singleplayerPanel;

    void Start()
    {
        multiplayerPanel.SetActive(false);
        hostingPanel.SetActive(false);
        singleplayerPanel.SetActive(false);

        mainMenuPanel.SetActive(true);
    }
}
