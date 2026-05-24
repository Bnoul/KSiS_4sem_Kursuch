using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class Single_player : MonoBehaviour
{
    public GameObject panel;
    public GameObject Main_menu;

    public TMP_Dropdown trackDropdown;
    public TMP_InputField lapsInput;
    public TextMeshProUGUI errorText;

    public void Open()
    {
        panel.SetActive(true);
    }

    public void Close()
    {
        Main_menu.SetActive(true);
        panel.SetActive(false);
    }

    public void StartSingleplayer()
    {
        errorText.text = "";

        if (!int.TryParse(lapsInput.text, out int laps) || laps <= 0)
        {
            errorText.text = "Ошибка: количество кругов должно быть > 0";
            return;
        }

        if (trackDropdown.options.Count == 0)
        {
            errorText.text = "Ошибка: список трасс пуст";
            return;
        }

        string track = trackDropdown.options[trackDropdown.value].text;

        PlayerPrefs.SetString("sp_track", track);
        PlayerPrefs.SetInt("sp_laps", laps);

        SceneManager.LoadScene(track);
    }
}
