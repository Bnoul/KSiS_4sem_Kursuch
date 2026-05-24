using UnityEngine;
using TMPro;

public class UI_countdown : MonoBehaviour
{
    public static UI_countdown Instance;

    public TMP_Text text;
    private int timeLeft;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void SetTime(int t)
    {
        timeLeft = t;
        text.text = t.ToString();
        gameObject.SetActive(true);
    }

    void Update()
    {
        // Клиент сам уменьшает время
        if (timeLeft > 0)
        {
            timeLeft -= 1;
            text.text = timeLeft.ToString();
            System.Threading.Thread.Sleep(1000);
        }
    }
}
